// <copyright file="ForkSecretsRedactionTests.cs" company="Robert S.">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) 2026 Robert S.
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, version 3.
//   │
//   │ This program is distributed in the hope that it will be useful,
//   │ but WITHOUT ANY WARRANTY; without even the implied warranty of
//   │ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   │ GNU Affero General Public License for more details.
//   │
//   │ You should have received a copy of the GNU Affero General Public License
//   │ along with this program.  If not, see https://www.gnu.org/licenses/.
//   │
//   │ This program is distributed with Additional Terms pursuant to Section 7
//   │ of the AGPLv3.  See the LICENSE file in the root directory of this
//   │ project for the complete terms and conditions.
//   │
//   │ https://slskd.org
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: 2026 Robert S.
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
// </copyright>

namespace slskd.Tests.Unit.Core.Options;

using System.Text.Json;
using Xunit;
using static slskd.Options;
using static slskd.Options.IntegrationsOptions;
using static slskd.Options.IntegrationsOptions.MailOptions;

/// <summary>
///     Read-only API keys may read the options, redacted (upstream, 64ff7f58), so every secret this fork's options
///     hold has to be one the redaction knows about.
/// </summary>
public class ForkSecretsRedactionTests
{
    private const string Marker = "do-not-leak-0123456789";

    [Fact]
    public void Redacted_Options_Carry_None_Of_This_Forks_Secrets()
    {
        var options = new slskd.Options
        {
            Player = new PlayerOptions
            {
                Analysis = new PlayerOptions.PlayerAnalysisOptions
                {
                    Provider = new PlayerOptions.PlayerAnalysisProviderOptions { Token = Marker + "-analysis" },
                },
            },
            Transfers = new TransfersOptions
            {
                Compare = new TransfersOptions.CompareOptions
                {
                    Provider = new TransfersOptions.CompareProviderOptions { Token = Marker + "-compare" },
                },
            },
            Integrations = new IntegrationsOptions
            {
                Mail = new MailOptions
                {
                    Smtp = new SmtpOptions { Password = Marker + "-smtp" },
                    Brevo = new BrevoOptions { ApiKey = Marker + "-brevo" },
                },
                Notifications = new NotificationsOptions
                {
                    Ntfy = new NtfyOptions { Url = "https://ntfy.example", Topic = Marker + "-topic", Token = Marker + "-ntfy" },
                },
            },
        };

        var json = JsonSerializer.Serialize(options.Redact());

        Assert.DoesNotContain(Marker, json);

        // and the redaction did not simply blank everything: a setting that is not a secret still reads back
        Assert.Contains("https://ntfy.example", json);
    }
}
