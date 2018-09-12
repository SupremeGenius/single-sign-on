﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Novell.Directory.Ldap;
using sh.vcp.ldap.Util;

namespace sh.vcp.ldap
{
    public abstract class LdapModel
    {
        protected static readonly string[] LoadProperties =
        {
            LdapProperties.CommonName,
            LdapProperties.ObjectClass
        };

        protected string ObjectClass;
        protected virtual string DefaultObjectClass => string.Empty;
        protected virtual Dictionary<PropertyInfo, LdapAttr> Properties => new Dictionary<PropertyInfo, LdapAttr>();

        protected LdapEntry Entry { get; private set; }

        /// <summary>
        ///     Id of the object (cn=*).
        /// </summary>
        [JsonProperty("Id")]
        [Required]
        public string Id { get; set; }

        /// <summary>
        ///     Domainname of the object.
        /// </summary>
        [JsonProperty("Dn")]
        [Required]
        public string Dn { get; set; }

        /// <summary>
        ///     Converts a ldap entry to the ldap model object.
        /// </summary>
        /// <param name="entry">Entry to convert.</param>
        public virtual void ProvideEntry(LdapEntry entry)
        {
            this.ObjectClass = entry.GetAttribute(LdapProperties.ObjectClass);
            this.Id = entry.GetAttribute(LdapProperties.CommonName);
            this.Dn = entry.DN;
            this.Entry = entry;

            // load properties with reflection
            foreach (KeyValuePair<PropertyInfo, LdapAttr> kv in this.Properties)
            {
                object value;
                switch (Type.GetTypeCode(kv.Value.Type))
                {
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                        value = entry.GetIntAttribute(kv.Value);
                        break;
                    case TypeCode.Boolean:
                        value = entry.GetBoolAttribute(kv.Value);
                        break;
                    case TypeCode.DateTime:
                        value = entry.GetDateTimeAttribute(kv.Value);
                        break;
                    case TypeCode.Object:
                        if (kv.Value.Type == typeof(List<string>))
                        {
                            value = entry.GetStringListAttribute(kv.Value);
                            break;
                        }

                        value = null;
                        break;
                    default:
                        value = entry.GetAttribute(kv.Value);
                        break;
                }

                kv.Key.SetValue(this, value);
            }
        }

        /// <summary>
        ///     Convert as a ldap model to the corresponding ldap attribute set.
        /// </summary>
        /// <param name="set"></param>
        /// <returns></returns>
        public virtual LdapAttributeSet GetAttributeSet(LdapAttributeSet set = null)
        {
            set = set ?? new LdapAttributeSet();
            set.Add(LdapProperties.ObjectClass, this.ObjectClass ?? this.DefaultObjectClass)
                .Add(LdapProperties.CommonName, this.Id);

            foreach (KeyValuePair<PropertyInfo, LdapAttr> kvp in this.Properties)
                set.Add(kvp.Value, kvp.Key.GetValue(this));

            return set;
        }

        protected virtual List<LdapModification> BLABLAB(List<LdapModification> list = null)
        {
            List<LdapModification> modifications = new List<LdapModification>();
            return modifications;
        }

        public LdapModification[] GetModifications()
        {
            List<LdapModification> modifications = new List<LdapModification>();
            foreach (KeyValuePair<PropertyInfo, LdapAttr> kv in this.Properties)
            {
                LdapModification mod = null;
                switch (Type.GetTypeCode(kv.Value.Type))
                {
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                    {
                        int? oldVal = this.Entry.GetIntAttribute(kv.Value);
                        int? newVal = (int?) kv.Key.GetValue(this);
                        if (newVal == null && oldVal != null)
                        {
                            mod = new LdapModification(LdapModification.DELETE,
                                this.Entry.getAttribute(kv.Value.LdapName));
                        }
                        else if (newVal != null && oldVal == null || newVal != oldVal)
                        {
                            mod = new LdapModification(
                                newVal == oldVal ? LdapModification.REPLACE : LdapModification.ADD,
                                kv.Value.CreateLdapAttribute(newVal));
                        }

                        break;
                    }
                    case TypeCode.Boolean:
                    {
                        bool? oldVal = this.Entry.GetBoolAttribute(kv.Value);
                        var newVal = (bool) kv.Key.GetValue(this);
                        if (oldVal == null || oldVal != newVal)
                        {
                            mod = new LdapModification(oldVal == null ? LdapModification.REPLACE : LdapModification.ADD,
                                kv.Value.CreateLdapAttribute(newVal));
                        }

                        break;
                    }
                    case TypeCode.DateTime:
                    {
                        DateTime? oldVal = this.Entry.GetDateTimeAttribute(kv.Value);
                        DateTime? newVal = (DateTime?) kv.Key.GetValue(this);
                        if (newVal == null && oldVal != null)
                        {
                            mod = new LdapModification(LdapModification.DELETE,
                                this.Entry.getAttribute(kv.Value.LdapName));
                        }
                        else if (newVal != null && oldVal == null)
                        {
                            mod = new LdapModification(LdapModification.ADD, kv.Value.CreateLdapAttribute(newVal));
                        }
                        else if (newVal.Value.CompareTo(oldVal.Value) != 0)
                        {
                            mod = new LdapModification(LdapModification.REPLACE, kv.Value.CreateLdapAttribute(newVal));
                        }

                        break;
                    }
                    case TypeCode.Object:
                    {
                        if (kv.Value.Type == typeof(List<string>))
                        {
                            List<string> oldValue = this.Entry.GetStringListAttribute(kv.Value);
                            List<string> newValue = (List<string>) kv.Key.GetValue(this);
                            var intersectCount = oldValue.Intersect(newValue).Count();
                            if (intersectCount != oldValue.Count || intersectCount != newValue.Count)
                            {
                                // find added member ids
                                newValue.ForEach(m =>
                                {
                                    if (oldValue.Contains(m)) return;
                                    var mmod = new LdapModification(LdapModification.ADD,
                                        new LdapAttribute(kv.Value.LdapName, m));
                                    modifications.Add(mmod);
                                });
                                // find removed member ids
                                oldValue.ForEach(m =>
                                {
                                    if (newValue.Contains(m)) return;
                                    var mmod = new LdapModification(LdapModification.DELETE,
                                        new LdapAttribute(kv.Value.LdapName, m));
                                    modifications.Add(mmod);
                                });
                            }
                        }

                        break;
                    }
                    default:
                    {
                        var oldValue = this.Entry.GetAttribute(kv.Value);
                        var newValue = kv.Key.GetValue(this);
                        if (newValue == null && oldValue != null)
                        {
                            mod = new LdapModification(LdapModification.DELETE,
                                this.Entry.getAttribute(kv.Value.LdapName));
                        }
                        else if (oldValue == null && newValue != null)
                        {
                            mod = new LdapModification(LdapModification.ADD, kv.Value.CreateLdapAttribute(newValue));
                        }
                        else if (oldValue != newValue)
                        {
                            mod = new LdapModification(LdapModification.REPLACE,
                                kv.Value.CreateLdapAttribute(newValue));
                        }

                        break;
                    }
                }

                if (mod != null) modifications.Add(mod);
            }

            return modifications.ToArray();
        }

        public LdapEntry ToEntry()
        {
            return new LdapEntry(this.Dn, this.GetAttributeSet());
        }
    }
}