﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Novell.Directory.Ldap;
using sh.vcp.ldap.ChangeTracking;
using sh.vcp.ldap.Exceptions;
using sh.vcp.ldap.Util;

namespace sh.vcp.ldap
{
    internal class LdapConnection : Novell.Directory.Ldap.LdapConnection, ILdapConnection
    {
        private readonly string _bindDn;
        private readonly string _bindPassword;
        private readonly LdapConfig _config;
        private readonly ILogger<LdapConnection> _logger;
        private readonly ChangeTrackingDbContext _trackingDbContext;
        private bool _connected;

        public LdapConnection(LdapConfig config, ILogger<LdapConnection> logger,
            ChangeTrackingDbContext trackingDbContext) {
            this._config = config ?? throw new ArgumentNullException(nameof(config));
            this._logger = logger;
            this._trackingDbContext = trackingDbContext;
        }

        private LdapConnection(LdapConfig config, ILogger<LdapConnection> logger, string dn, string password) {
            this._logger = logger;
            this._config = config ?? throw new ArgumentNullException(nameof(config));
            this._bindDn = dn ?? throw new ArgumentNullException(nameof(dn));
            this._bindPassword = password ?? throw new ArgumentNullException(nameof(password));
        }

        public async Task<TModel> SearchFirst<TModel>(string baseDn, string filter, string objectClass, int scope,
            string[] attributes,
            bool expectUnique, CancellationToken cancellationToken) where TModel : LdapModel, new() {
            if (objectClass == null) throw new ArgumentNullException(nameof(objectClass));
            return await Task.Run(async () => {
                if (!this._connected) this.Connect();

                var queue = this.Search(baseDn, scope, filter ?? $"{LdapProperties.ObjectClass}={objectClass}",
                    attributes,
                    false);
                List<TModel> entries = new List<TModel>();
                while (queue.HasMore()) {
                    var m = new TModel();
                    m.ProvideEntry(queue.Next());
                    if (typeof(ILdapModelWithChildren).IsAssignableFrom(typeof(TModel)))
                        await ((ILdapModelWithChildren) m).LoadChildren(this, cancellationToken);
                    entries.Add(m);
                }

                if (expectUnique && entries.Count > 1) throw new LdapSearchNotUniqueException(filter, queue.Count);

                return entries.FirstOrDefault();
            }, cancellationToken);
        }

        public async Task<ICollection<TModel>> Search<TModel>(string baseDn, string filter, string objectClass,
            int scope, string[] attributes,
            CancellationToken cancellationToken = default) where TModel : LdapModel, new() {
            if (objectClass == null) throw new ArgumentNullException(nameof(objectClass));
            return await Task.Run(async () => {
                if (!this._connected) this.Connect();

                filter = string.IsNullOrEmpty(filter)
                    ? $"{LdapProperties.ObjectClass}={objectClass}"
                    : $"(&({LdapProperties.ObjectClass}={objectClass})({filter}))";
                var queue = this.Search(baseDn, scope, filter, attributes,
                    false);
                ICollection<TModel> entries = new List<TModel>();
                while (queue.HasMore()) {
                    var m = new TModel();
                    m.ProvideEntry(queue.Next());
                    if (typeof(ILdapModelWithChildren).IsAssignableFrom(typeof(TModel)))
                        await ((ILdapModelWithChildren) m).LoadChildren(this, cancellationToken);
                    entries.Add(m);
                }

                return entries;
            }, cancellationToken);
        }

        public Task<TModel> Read<TModel>(string dn, CancellationToken cancellationToken = default)
            where TModel : LdapModel, new() {
            return Task.Run(async () => {
                try {
                    if (!this._connected) this.Connect();

                    var entry = this.Read(dn);
                    var m = new TModel();
                    m.ProvideEntry(entry);
                    if (typeof(ILdapModelWithChildren).IsAssignableFrom(typeof(TModel)))
                        await ((ILdapModelWithChildren) m).LoadChildren(this, cancellationToken);
                    return m;
                }
                catch (LdapException ex) {
                    if (ex.ResultCode == 34) {
                        var ldapEx = new LdapDnInvalidException(dn);
                        this._logger.LogError(ldapEx, LdapLogCodes.LdapReadError);
                        throw ldapEx;
                    }

                    this._logger.LogError(ex, LdapLogCodes.LdapReadError);
                    return null;
                }
            }, cancellationToken);
        }

        public Task<bool> Bind(string dn, string password, CancellationToken cancellationToken) {
            return Task.Run(() => {
                try {
                    using (var con = new LdapConnection(this._config, this._logger, dn, password)) {
                        con.Connect();
                        return con.Bound;
                    }
                }
                catch (LdapException ex) {
                    this._logger.LogError(ex, LdapLogCodes.LdapBindError);
                    this._connected = false;
                }

                return false;
            }, cancellationToken);
        }

        public async Task<TModel> Add<TModel>(TModel model, CancellationToken cancellationToken) where TModel : LdapModel {
            try {
                if (!this._connected) this.Connect();
                var entry = model.ToEntry();
                this.Add(entry);
                if (this._config.LogChanges) {
                    IEnumerable<Change> changes = entry.getAttributeSet().ToChangesAdd(model.Dn);
                    await this._trackingDbContext.AddRangeAsync(changes, cancellationToken);
                    await this._trackingDbContext.SaveChangesAsync(cancellationToken);
                }
                return model;
            }
            catch (LdapException ex) {
                switch (ex.ResultCode) {
                    case 68: {
                        throw new Exception("Entry already exists");
                    }
                    default:
                        this._logger.LogError(ex, LdapLogCodes.LdapAddError);
                        throw new Exception($"Adding {model.Dn} failed");
                }
            }
        }

        public async Task<TModel> AddChildren<TModel>(TModel model, CancellationToken cancellationToken)
            where TModel : LdapModel, ILdapModelWithChildren {
            try {
                foreach (var ldapModel in model.GetChildren()) await this.Add(ldapModel, cancellationToken);
                return model;
            }
            catch (LdapException ex) {
                this._logger.LogError(ex, LdapLogCodes.LdapAddChildrenError);
                return null;
            }
        }

        public Task<bool> Update<TModel>(TModel model, CancellationToken cancellationToken) where TModel : LdapModel, new() {
            return Task.Run(() => this.Update<TModel>(model.Dn, model.GetModifications(), cancellationToken),
                cancellationToken);
        }

        public async Task<bool> Update<TModel>(string dn, LdapModification[] ldapModifications,
            CancellationToken cancellationToken = default) where TModel: LdapModel, new()  {
                try {
                    if (ldapModifications.Length <= 0) return true;
                    if (!this._connected) this.Connect();

                    this.Modify(dn, ldapModifications);
                    
                    if (this._config.LogChanges) {
                        var oldObject = await this.Read<TModel>(dn, cancellationToken);
                        IEnumerable<Change> changes = ldapModifications.ToChangesModify(dn, oldObject.ObjectClass);
                        await this._trackingDbContext.AddRangeAsync(changes, cancellationToken);
                        await this._trackingDbContext.SaveChangesAsync(cancellationToken);
                    }
                    return true;
                }
                catch (Exception ex) {
                    this._logger.LogError(ex, LdapLogCodes.LdapModifyError);
                    return false;
                }
        }

        private void Connect() {
            if (this._connected) return;
            try {
                this.Connect(this._config.Hostname, this._config.Port);
                this.Bind(this._bindDn ?? this._config.AdminUserDn,
                    this._bindPassword ?? this._config.AdminUserPassword);
                this._connected = true;
            }
            catch (LdapException ex) {
                this._logger.LogError(ex, LdapLogCodes.LdapConnectError);
                this._connected = false;
            }
        }
    }
}