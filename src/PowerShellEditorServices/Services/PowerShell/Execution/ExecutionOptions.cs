﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.PowerShell.EditorServices.Services.PowerShell.Execution
{
    public enum ExecutionPriority
    {
        Normal,
        Next,
    }

    // Some of the fields of this class are not orthogonal,
    // so it's possible to construct self-contradictory execution options.
    // We should see if it's possible to rework this class to make the options less misconfigurable.
    // Generally the executor will do the right thing though; some options just priority over others.
    public record ExecutionOptions
    {
        public static ExecutionOptions Default = new()
        {
            Priority = ExecutionPriority.Normal,
            MustRunInForeground = false,
            InterruptCurrentForeground = false,
        };

        public ExecutionPriority Priority { get; init; }

        public bool MustRunInForeground { get; init; }

        public bool InterruptCurrentForeground { get; init; }
    }

    public record PowerShellExecutionOptions : ExecutionOptions
    {
        public static new PowerShellExecutionOptions Default = new()
        {
            Priority = ExecutionPriority.Normal,
            MustRunInForeground = false,
            InterruptCurrentForeground = false,
            WriteOutputToHost = false,
            WriteInputToHost = false,
            ThrowOnError = true,
            AddToHistory = false,
        };

        public bool WriteOutputToHost { get; init; }

        public bool WriteInputToHost { get; init; }

        public bool ThrowOnError { get; init; }

        public bool AddToHistory { get; init; }
    }
}
