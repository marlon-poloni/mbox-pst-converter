// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later
#nullable enable
using System;
using System.IO;
using System.Linq;
using Mail2Pst.Core.Parsing;
using Xunit;

namespace Mail2Pst.Core.Tests.Fuzzing;

/// <summary>
/// Regression home for crashes surfaced by tools/Mail2Pst.Fuzz. Each test feeds a synthetic,
/// PII-free input and asserts GRACEFUL degradation (a whitelisted outcome), never an un-whitelisted
/// throw. New fuzzer finds are added here as minimized synthetic repros.
/// </summary>
public class FuzzGracefulDegradationTests
{
    // Lock: arbitrary non-mbox garbage bytes must enumerate to completion without an unexpected
    // throw out of Parse. NOTE: garbage with no `From ` boundary is simply not an mbox message, so
    // this may yield ZERO results — the invariant is "enumeration completes, no un-whitelisted
    // exception", NOT "produces failed skips". This is the exact contract the mbox fuzz target checks.
    [Fact]
    public void MboxParser_GarbageBytes_EnumeratesToCompletion_NoUnexpectedThrow()
    {
        byte[] garbage = Enumerable.Range(0, 4096).Select(i => (byte)((i * 37) ^ 0xA5)).ToArray();
        string path = Path.Combine(Path.GetTempPath(), $"m2p-fuzzreg-{Guid.NewGuid():N}.mbox");
        File.WriteAllBytes(path, garbage);
        try
        {
            // Primary guarantee: enumerating to completion does not throw an un-whitelisted
            // exception. Garbage with no `From ` boundary may legitimately yield ZERO results.
            var results = new MboxParser().Parse(path).ToList();   // must not throw; may be empty
            // Real (non-tautological) shape invariant: a successful result always carries a Message.
            Assert.All(results, r => Assert.True(!r.Success || r.Message is not null));
        }
        finally { File.Delete(path); }
    }

}
