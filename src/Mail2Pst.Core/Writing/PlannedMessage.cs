// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System;
using System.Collections.Generic;
using Mail2Pst.Core.Models;

namespace Mail2Pst.Core.Writing;

public class PlannedMessage
{
    public MailMessage Message { get; set; } = new();
    public IReadOnlyList<string> TargetFolderPath { get; set; } = System.Array.Empty<string>();

    private IReadOnlyList<IReadOnlyList<string>>? _targetFolderPaths;

    public IReadOnlyList<IReadOnlyList<string>> TargetFolderPaths
    {
        get => _targetFolderPaths ?? new[] { TargetFolderPath };
        set => _targetFolderPaths = value;
    }
}
