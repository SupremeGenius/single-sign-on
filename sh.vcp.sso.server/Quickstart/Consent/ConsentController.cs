﻿// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using IdentityServer4.Models;
using IdentityServer4.Services;
using IdentityServer4.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace IdentityServer4.Quickstart.UI
{
    /// <summary>
    /// This controller processes the consent UI
    /// </summary>
    [SecurityHeaders]
    [Authorize]
    public class ConsentController : Controller
    {
        private readonly IIdentityServerInteractionService _interaction;
        private readonly IClientStore _clientStore;
        private readonly IResourceStore _resourceStore;
        private readonly ILogger<ConsentController> _logger;

        public ConsentController(
            IIdentityServerInteractionService interaction,
            IClientStore clientStore,
            IResourceStore resourceStore,
            ILogger<ConsentController> logger) {
            this._interaction = interaction;
            this._clientStore = clientStore;
            this._resourceStore = resourceStore;
            this._logger = logger;
        }

        /// <summary>
        /// Shows the consent screen
        /// </summary>
        /// <param name="returnUrl"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> Index(string returnUrl) {
            var vm = await this.BuildViewModelAsync(returnUrl);
            if (vm != null) {
                return this.View("Index", vm);
            }

            return this.View("Error");
        }

        /// <summary>
        /// Handles the consent screen postback
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Index(ConsentInputModel model) {
            var result = await this.ProcessConsent(model);

            if (result.IsRedirect) {
                return this.Redirect(result.RedirectUri);
            }

            if (result.HasValidationError) {
                this.ModelState.AddModelError("", result.ValidationError);
            }

            if (result.ShowView) {
                return this.View("Index", result.ViewModel);
            }

            return this.View("Error");
        }

        /*****************************************/
        /* helper APIs for the ConsentController */
        /*****************************************/
        private async Task<ProcessConsentResult> ProcessConsent(ConsentInputModel model) {
            var result = new ProcessConsentResult();

            ConsentResponse grantedConsent = null;

            switch (model.Button) {
                // user clicked 'no' - send back the standard 'access_denied' response
                // user clicked 'yes' - validate the data
                case "no":
                    grantedConsent = ConsentResponse.Denied;
                    ;
                    break;
                case "yes" when model != null:
                {
                    // if the user consented to some scope, build the response model
                    if (model.ScopesConsented != null && model.ScopesConsented.Any()) {
                        var scopes = model.ScopesConsented;
                        if (ConsentOptions.EnableOfflineAccess == false) {
                            scopes = scopes.Where(x =>
                                x != IdentityServer4.IdentityServerConstants.StandardScopes.OfflineAccess);
                        }

                        grantedConsent = new ConsentResponse {
                            RememberConsent = model.RememberConsent,
                            ScopesConsented = scopes.ToArray()
                        };
                    }
                    else {
                        result.ValidationError = ConsentOptions.MustChooseOneErrorMessage;
                    }

                    break;
                }
                default:
                    result.ValidationError = ConsentOptions.InvalidSelectionErrorMessage;
                    break;
            }

            if (grantedConsent != null) {
                // validate return url is still valid
                var request = await this._interaction.GetAuthorizationContextAsync(model.ReturnUrl);
                if (request == null) return result;

                // communicate outcome of consent back to identityserver
                await this._interaction.GrantConsentAsync(request, grantedConsent);

                // indicate that's it ok to redirect back to authorization endpoint
                result.RedirectUri = model.ReturnUrl;
            }
            else {
                // we need to redisplay the consent UI
                result.ViewModel = await this.BuildViewModelAsync(model.ReturnUrl, model);
            }

            return result;
        }

        private async Task<ConsentViewModel> BuildViewModelAsync(string returnUrl, ConsentInputModel model = null) {
            var request = await this._interaction.GetAuthorizationContextAsync(returnUrl);
            if (request != null) {
                var client = await this._clientStore.FindEnabledClientByIdAsync(request.ClientId);
                if (client != null) {
                    var resources = await this._resourceStore.FindEnabledResourcesByScopeAsync(request.ScopesRequested);
                    if (resources != null && (resources.IdentityResources.Any() || resources.ApiResources.Any())) {
                        return this.CreateConsentViewModel(model, returnUrl, request, client, resources);
                    }
                    else {
                        this._logger.LogError("No scopes matching: {0}",
                            request.ScopesRequested.Aggregate((x, y) => x + ", " + y));
                    }
                }
                else {
                    this._logger.LogError("Invalid client id: {0}", request.ClientId);
                }
            }
            else {
                this._logger.LogError("No consent request matching request: {0}", returnUrl);
            }

            return null;
        }

        private ConsentViewModel CreateConsentViewModel(
            ConsentInputModel model, string returnUrl,
            AuthorizationRequest request,
            Client client, Resources resources) {
            var vm = new ConsentViewModel
            {
                RememberConsent = model?.RememberConsent ?? true,
                ScopesConsented = model?.ScopesConsented ?? Enumerable.Empty<string>(),
                ReturnUrl = returnUrl,
                ClientName = client.ClientName ?? client.ClientId,
                ClientUrl = client.ClientUri,
                ClientLogoUrl = client.LogoUri,
                AllowRememberConsent = client.AllowRememberConsent
            };
            
            vm.IdentityScopes = resources.IdentityResources.Select(x =>
                this.CreateScopeViewModel(x, vm.ScopesConsented.Contains(x.Name) || model == null)).ToArray();
            vm.ResourceScopes = resources.ApiResources.SelectMany(x => x.Scopes).Select(x =>
                this.CreateScopeViewModel(x, vm.ScopesConsented.Contains(x.Name) || model == null)).ToArray();
            if (ConsentOptions.EnableOfflineAccess && resources.OfflineAccess) {
                vm.ResourceScopes = vm.ResourceScopes.Union(new ScopeViewModel[] {
                    this.GetOfflineAccessScope(
                        vm.ScopesConsented.Contains(
                            IdentityServer4.IdentityServerConstants.StandardScopes.OfflineAccess) || model == null)
                });
            }

            return vm;
        }

        private ScopeViewModel CreateScopeViewModel(IdentityResource identity, bool check) {
            return new ScopeViewModel {
                Name = identity.Name,
                DisplayName = identity.DisplayName,
                Description = identity.Description,
                Emphasize = identity.Emphasize,
                Required = identity.Required,
                Checked = check || identity.Required
            };
        }

        public ScopeViewModel CreateScopeViewModel(Scope scope, bool check) {
            return new ScopeViewModel {
                Name = scope.Name,
                DisplayName = scope.DisplayName,
                Description = scope.Description,
                Emphasize = scope.Emphasize,
                Required = scope.Required,
                Checked = check || scope.Required
            };
        }

        private ScopeViewModel GetOfflineAccessScope(bool check) {
            return new ScopeViewModel {
                Name = IdentityServer4.IdentityServerConstants.StandardScopes.OfflineAccess,
                DisplayName = ConsentOptions.OfflineAccessDisplayName,
                Description = ConsentOptions.OfflineAccessDescription,
                Emphasize = true,
                Checked = check
            };
        }
    }
}
