﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

using CMS.EventLog;
using CMS.Localization;
using CMS.SiteProvider;
using CMS.Taxonomy;

namespace Kentico.KenticoCloudPublishing
{
    internal partial class LanguageSync : SyncBase
    {
        public LanguageSync(SyncSettings settings) : base(settings)
        {
        }

        #region "External IDs"

        public static string GetLanguageExternalId(string cultureCode)
        {
            return $"language|{cultureCode}";
        }

        #endregion

        #region "Synchronization"

        public bool IsAtSynchronizedSite(CultureInfo culture)
        {
            var siteId = SiteInfoProvider.GetSiteID(Settings.Sitename);

            return CultureSiteInfoProvider.GetCultureSiteInfo(culture.CultureID, siteId) != null;
        }

        private async Task<List<LanguageData>> GetAllLanguages(string continuationToken = null)
        {
            var query = (continuationToken != null) ? "?continuationToken=" + HttpUtility.UrlEncode(continuationToken) : "";
            var endpoint = $"/languages{query}";

            var response = await ExecuteWithResponse<LanguagesResponse>(endpoint, HttpMethod.Get);
            if (response == null)
            {
                return new List<LanguageData>();
            }

            var languages = response.Languages;

            if ((response.Pagination != null) && !string.IsNullOrEmpty(response.Pagination.ContinuationToken))
            {
                var nextIds = await GetAllLanguages(response.Pagination.ContinuationToken);
                languages = languages.Concat(nextIds);
            }

            return languages.ToList();
        }

        public async Task SyncCultures(CancellationToken? cancellation = null)
        {
            try
            {
                SyncLog.Log("Synchronizing cultures");

                SyncLog.LogEvent(EventType.INFORMATION, "KenticoCloudPublishing", "ENSURECULTURES");

                var existingLanguages = await GetAllLanguages();
                var cultures = CultureSiteInfoProvider.GetSiteCultures(Settings.Sitename);

                var missingCultures = cultures.Where(
                    // Culture code name is case sensitive, it must be exact
                    culture => !existingLanguages.Exists(language => language.Codename.Equals(culture.CultureCode))
                );

                foreach (var culture in missingCultures)
                {
                    await CreateLanguage(culture);
                }
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoCloudPublishing", "ENSURECULTURES", ex);
                throw;
            }
        }

        private async Task CreateLanguage(CultureInfo culture)
        {
            try
            {
                SyncLog.LogEvent(EventType.INFORMATION, "KenticoCloudPublishing", "CREATECULTURE", $"{culture.CultureName} ({culture.CultureCode})");

                var externalId = GetLanguageExternalId(culture.CultureCode);
                var endpoint = $"/languages";

                var payload = new
                {
                    name = culture.CultureName,
                    codename = culture.CultureCode,
                    external_id = externalId,
                    is_active = true,
                    // Default language is always empty, and no fallback is used as a result
                    // fallback_language = new { id = Guid.Empty }
                };

                await ExecuteWithoutResponse(endpoint, HttpMethod.Post, payload);
            }
            catch (Exception ex)
            {
                SyncLog.LogException("KenticoCloudPublishing", "CREATECULTURE", ex);
                throw;
            }
        }

        #endregion
    }
}