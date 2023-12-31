﻿namespace Skyline.Protocol.PollManager.ResponseHandler.Repositories
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

    using Newtonsoft.Json;

    using Skyline.DataMiner.Scripting;
    using Skyline.DataMiner.Utils.Github.API.V20221128.Repositories;
    using Skyline.Protocol;
    using Skyline.Protocol.API.Headers;
    using Skyline.Protocol.Extensions;
    using Skyline.Protocol.PollManager.RequestHandler.Repositories;
    using Skyline.Protocol.Tables;

    public static partial class RepositoriesResponseHandler
    {
        public static void HandleRepositoriesReleasesResponse(SLProtocol protocol)
        {
            // Check status code
            if (!protocol.IsSuccessStatusCode())
            {
                return;
            }

            // Parse response
            var response = JsonConvert.DeserializeObject<List<RepositoryReleasesResponse>>(Convert.ToString(protocol.GetParameter(Parameter.getrepositoryreleasescontent)));

            if (response == null)
            {
                protocol.Log($"QA{protocol.QActionID}|ParseGetRepositoryReleasesResponse|response was null.", LogType.Error, LogLevel.NoLogging);
                return;
            }

            if (!response.Any())
            {
                // No releases for the repository
                return;
            }

            // Parse url to check which respository this issue is linked to
            var pattern = "https:\\/\\/api.github.com\\/repos\\/(.*)\\/(.*)\\/releases\\/(\\d+)";
            var options = RegexOptions.Multiline;

            var match = Regex.Match(response[0]?.Url, pattern, options);
            var owner = match.Groups[1].Value;
            var name = match.Groups[2].Value;

            // Update the releases table
            var table = new RepositoryReleasesRecords();
            foreach (var release in response)
            {
                if (release == null)
                {
                    protocol.Log($"QA{protocol.QActionID}|GetRepositoryReleasesResponse|Release was null.", LogType.Information, LogLevel.NoLogging);
                    continue;
                }

                if (release.Url == null)
                {
                    protocol.Log($"QA{protocol.QActionID}|GetRepositoryReleasesResponse|Release url null.", LogType.Information, LogLevel.NoLogging);
                    continue;
                }

                table.Rows.Add(new RepositoryReleasesRecord
                {
                    Instance = $"{owner}/{name}/releases/{release.Id}",
                    ID = release.Id,
                    TagName = release.TagName ?? Exceptions.NotAvailable,
                    TagId = release.TagName != null ? $"{owner}/{name}/commits/{release.TagName}" : Exceptions.NotAvailable,
                    TargetCommitish = release.TargetCommitish,
                    Name = release.Name,
                    Draft = release.Draft,
                    PreRelease = release.Prerelease,
                    Body = release.Body,
                    Author = release.Author?.Login ?? Exceptions.NotAvailable,
                    CreatedAt = release.CreatedAt,
                    PublishedAt = release.PublishedAt,
                    RepositoryId = $"{owner}/{name}",
                });
            }

            if (table.Rows.Count > 0)
            {
                table.SaveToProtocol(protocol, true);
            }

            protocol.Log($"QA{protocol.QActionID}|ParseGetRepositoryReleasesResponse|Release repo: {owner}/{name}", LogType.DebugInfo, LogLevel.NoLogging);

            // Check if there are more releases to fetch
            var linkHeader = Convert.ToString(protocol.GetParameter(Parameter.getrepositoryreleaseslinkheader));
            if (string.IsNullOrEmpty(linkHeader)) return;

            var link = new LinkHeader(linkHeader);

            protocol.Log($"QA{protocol.QActionID}|ParseGetRepositoryReleasesResponse|Current page: {link.CurrentPage}", LogType.DebugInfo, LogLevel.NoLogging);
            protocol.Log($"QA{protocol.QActionID}|ParseGetRepositoryReleasesResponse|Has next page: {link.HasNext}", LogType.DebugInfo, LogLevel.NoLogging);

            if (link.HasNext)
            {
                RepositoriesRequestHandler.HandleRepositoriesReleasesRequest(protocol, owner, name, PollingConstants.PerPage, link.NextPage);
            }
        }
    }
}
