// SPDX-FileCopyrightText: 2026 Aksel Visby (ContinuMail)
// SPDX-License-Identifier: GPL-3.0-or-later

#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mail2Pst.Core.Config;
using Mail2Pst.Core;

namespace Mail2Pst.Core.Mapping;

public static class MappingEngine
{
    /// <summary>
    /// Folder name used for sources in a "flatten" group that don't have a
    /// per-source <see cref="SourceConfig.TargetFolder"/> override.
    /// </summary>
    public const string DefaultFlattenFolderName = "Imported Mail";

    public static List<PstOutputPlan> BuildPlan(ConversionConfig config)
    {
        var plans = new List<PstOutputPlan>();

        foreach (OutputGroupConfig output in config.Outputs)
        {
            var plan = new PstOutputPlan
            {
                Name = output.Name,
                MaxSizeBytes = checked(output.MaxSizeMB * 1024L * 1024L),
                IncludeEmptyFolders = output.IncludeEmptyFolders,
            };

            var usedMailKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SourceConfig source in output.Sources ?? new List<SourceConfig>())
            {
                IReadOnlyList<string> targetPath;
                bool mirrorDerived = false;
                if (source.TargetFolderPath is not null)
                    // An explicit path (even if empty/invalid) is treated as explicit — never
                    // silently falls back to a mode default. Invalid explicit targets are rejected
                    // up front by ConfigValidator in the real pipeline, and by GetOrCreateFolder's
                    // guard at write time; we do NOT mask them here by reverting to mode behaviour.
                    targetPath = source.TargetFolderPath.ToArray();        // copy: immutable snapshot
                else if (source.TargetFolder is not null)
                    // Same rule for the flat shorthand: an explicit (even empty) TargetFolder stays
                    // explicit. Using `is not null` (not IsNullOrEmpty) so an invalid "" is not
                    // silently converted into a mirror/flatten default.
                    targetPath = new[] { source.TargetFolder };
                else if (output.FolderMapping == FolderMappingMode.Mirror)
                {
                    // Mirror: one folder per source file. Sanitize the filename stem (may be reserved,
                    // padded, or empty for "/x/.mbox") so it can't fail ConfigValidator / CreateChildFolder,
                    // and warn whenever sanitizing changed it — a silent rename must never happen.
                    string rawStem = Path.GetFileNameWithoutExtension(source.Path);
                    string sanitizedStem = FolderNameValidator.Sanitize(rawStem, "Folder");
                    if (!string.Equals(sanitizedStem, rawStem, StringComparison.Ordinal))
                        plan.PlanningWarnings.Add(
                            $"Source '{source.Path}' has an invalid mirror folder name '{rawStem}' — using '{sanitizedStem}'.");
                    targetPath = new[] { sanitizedStem };
                    mirrorDerived = true;
                }
                else
                    targetPath = new[] { DefaultFlattenFolderName };        // flatten — intentional merge

                // Mirror-derived paths can silently collide (Project.v1 and Project.v2 both -> "Project",
                // or two sources sanitizing to the same fallback). Keep the first, suffix later ones
                // " (2)", " (3)", … (first free), and record a planning warning. Explicit/flatten targets
                // are left as-is — their merges are intentional.
                if (mirrorDerived && usedMailKeys.Contains(FolderPathKey.Join(targetPath)))
                {
                    string baseLeaf = targetPath[^1];
                    int n = 2;
                    string leaf;
                    do { leaf = $"{baseLeaf} ({n})"; n++; }
                    while (usedMailKeys.Contains(FolderPathKey.Join(new[] { leaf })));
                    plan.PlanningWarnings.Add(
                        $"Source '{source.Path}' maps to mail folder '{baseLeaf}', already used — using '{leaf}' instead.");
                    targetPath = new[] { leaf };
                }
                usedMailKeys.Add(FolderPathKey.Join(targetPath));

                plan.SourceMappings.Add(new SourceMapping { Source = source, TargetFolderPath = targetPath });
            }

            plans.Add(plan);
        }

        return plans;
    }
}
