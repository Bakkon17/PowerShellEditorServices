// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.PowerShell.EditorServices.Handlers
{
    using System.Management.Automation;
    using Microsoft.PowerShell.EditorServices.Services.PowerShell;
    using Microsoft.PowerShell.EditorServices.Services.PowerShell.Runspace;

    internal class PSHostProcessAndRunspaceHandlers : IGetPSHostProcessesHandler, IGetRunspaceHandler
    {
        private readonly ILogger<PSHostProcessAndRunspaceHandlers> _logger;
        private readonly IInternalPowerShellExecutionService _executionService;
        private readonly IRunspaceContext _runspaceContext;
        private static readonly int currentProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;

        public PSHostProcessAndRunspaceHandlers(
            ILoggerFactory factory,
            IInternalPowerShellExecutionService executionService,
            IRunspaceContext runspaceContext)
        {
            _logger = factory.CreateLogger<PSHostProcessAndRunspaceHandlers>();
            _executionService = executionService;
            _runspaceContext = runspaceContext;
        }

        public async Task<PSHostProcessResponse[]> Handle(GetPSHostProcessesParams request, CancellationToken cancellationToken)
        {
            PSCommand psCommand = new PSCommand().AddCommand(@"Microsoft.PowerShell.Core\Get-PSHostProcessInfo");
            IReadOnlyList<PSObject> processes = await _executionService.ExecutePSCommandAsync<PSObject>(
                psCommand, cancellationToken).ConfigureAwait(false);

            List<PSHostProcessResponse> psHostProcesses = [];
            foreach (dynamic p in processes)
            {
                PSHostProcessResponse response = new()
                {
                    ProcessName = p.ProcessName,
                    ProcessId = p.ProcessId,
                    AppDomainName = p.AppDomainName,
                    MainWindowTitle = p.MainWindowTitle
                };

                // NOTE: We do not currently support attaching to ourself in this manner, so we
                // exclude our process. When we maybe eventually do, we should name it.
                if (response.ProcessId == currentProcessId) {
                    continue;
                }

                psHostProcesses.Add(response);
            }

            return psHostProcesses.ToArray();
        }

        public async Task<RunspaceResponse[]> Handle(GetRunspaceParams request, CancellationToken cancellationToken)
        {
            IReadOnlyList<PSObject> runspaces = [];

            // If we're the host process we just use Get-Runspace.
            if (request.ProcessId == currentProcessId)
            {
                PSCommand psCommand = new PSCommand().AddCommand(@"Microsoft.PowerShell.Utility\Get-Runspace");
                // returns (not deserialized) Runspaces. For simpler code, we use PSObject and rely on dynamic later.
                runspaces = await _executionService.ExecutePSCommandAsync<PSObject>(psCommand, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Create a remote runspace that we will invoke Get-Runspace in.
                using Runspace rs = RunspaceFactory.CreateRunspace(new NamedPipeConnectionInfo(request.ProcessId));
                using PowerShell ps = PowerShell.Create();
                rs.Open();
                ps.Runspace = rs;
                // Returns deserialized Runspaces. For simpler code, we use PSObject and rely on dynamic later.
                runspaces = ps.AddCommand(@"Microsoft.PowerShell.Utility\Get-Runspace").Invoke<PSObject>();
            }

            List<RunspaceResponse> runspaceResponses = [];
            foreach (dynamic runspace in runspaces)
            {
                // When we are attached to ourself, we cannot include ConsoleHost (Id 1) nor the
                // current runspace.
                //
                // NOTE: Attaching to ourself doesn't currently work at all, but if it did, we would
                // need to do this.
                if (request.ProcessId == currentProcessId
                    && (runspace.Id == 1 || runspace.Id == _runspaceContext.CurrentRunspace.Runspace.Id))
                {
                    continue;
                }

                runspaceResponses.Add(
                    new RunspaceResponse
                    {
                        Id = runspace.Id,
                        Name = runspace.Name,
                        Availability = runspace.RunspaceAvailability.ToString()
                    });
            }

            return runspaceResponses.ToArray();
        }
    }
}
