// <copyright file="EndpointRoleTests.cs" company="Robert S.">
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

namespace slskd.Tests.Unit.Common.Authentication;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using slskd.Authentication;
using Xunit;

/// <summary>
///     The rule upstream applied to its endpoints, held for every endpoint: anything that changes state needs at
///     least read/write access. A read-only API key may look, never act.
/// </summary>
public class EndpointRoleTests
{
    private static readonly string[] ChangingMethods = ["POST", "PUT", "PATCH", "DELETE"];

    public static IEnumerable<object[]> ChangingActions()
        => typeof(Program).Assembly.GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type) && !type.IsAbstract)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().SelectMany(a => a.HttpMethods).Any(m => ChangingMethods.Contains(m)))
                .Select(method => new object[] { $"{type.Name}.{method.Name}" }))
            .Distinct();

    [Theory]
    [MemberData(nameof(ChangingActions))]
    public void A_Changing_Endpoint_Refuses_A_Read_Only_Caller(string action)
    {
        var (type, method) = Find(action);

        if (method.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
        {
            // anonymous on purpose -- logging in, and the like -- and so not a question of roles
            return;
        }

        // roles on the action decide; failing that, the controller's
        var roles = method.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Roles).FirstOrDefault(r => r is not null)
            ?? type.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Roles).FirstOrDefault(r => r is not null);

        Assert.False(roles is null, $"{action} changes state but names no roles, so a read-only key may call it");
        Assert.DoesNotContain(nameof(Role.ReadOnly), roles.Split(','));
    }

    [Fact]
    public void Finds_The_Endpoints_It_Is_Meant_To_Hold()
    {
        var actions = ChangingActions().Select(a => (string)a[0]).ToList();

        Assert.Contains("ShareLinksController.Create", actions);
        Assert.Contains("WatchesController.Run", actions);
        Assert.Contains("TransfersController.CancelDownloadAsync", actions);
    }

    private static (Type Type, MethodInfo Method) Find(string action)
    {
        var parts = action.Split('.');
        var type = typeof(Program).Assembly.GetTypes().Single(t => t.Name == parts[0] && typeof(ControllerBase).IsAssignableFrom(t));
        var method = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .First(m => m.Name == parts[1] && m.GetCustomAttributes<HttpMethodAttribute>().Any());

        return (type, method);
    }
}
