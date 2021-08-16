using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Serilog;
using Stateless;

namespace StatefulWorker
{
    public class WorkerService : BackgroundService
    {
        private static StateMachine<State, Trigger> _stateMachine;
        
        protected override async Task ExecuteAsync(CancellationToken stopToken)
        {
            Log.Debug("Configuring state machine...");
            
            _stateMachine = new StateMachine<State, Trigger>(State.Startup);

            _stateMachine
                .Configure(State.Startup)
                .OnActivateAsync(async () => await Start(stopToken))
                .Permit(Trigger.Start, State.WaitingForReset);

            _stateMachine
                .Configure(State.WaitingForReset)
                .OnEntryAsync(async () => await ListenForFeatureToggleToUnset(stopToken))
                .Permit(Trigger.Reset, State.WaitingToRun);

            _stateMachine
                .Configure(State.WaitingToRun)
                .OnEntryAsync(async () => await ListenForFeatureToggleToSet(stopToken))
                .Permit(Trigger.Set, State.Running);

            _stateMachine
                .Configure(State.Running)
                .OnEntryAsync(async () => await RunJob(stopToken))
                .Permit(Trigger.JobFinished, State.WaitingForReset);
            
            Log.Debug("State machine configured.");
            
            Log.Debug("Activating state machine...");

            Task.Run(async () => await _stateMachine.ActivateAsync());
            
            Log.Debug("State machine activated.");
        }

        private async Task Start(CancellationToken stopToken)
        {
            Log.Information("{state}: Entered: '{method_name}'", _stateMachine.State, nameof(Start));
            
            if (stopToken.IsCancellationRequested) return;
            Log.Information("{state}: Firing trigger: 'Start'", _stateMachine.State);
            await _stateMachine.FireAsync(Trigger.Start);
        }

        private async Task ListenForFeatureToggleToUnset(CancellationToken stopToken)
        {
            Log.Information("{state}: Entered: '{method_name}'", _stateMachine.State, nameof(ListenForFeatureToggleToUnset));
            Log.Debug("Listening For feature toggle to un-set");
            var sleepSeconds = 15;
            while (!stopToken.IsCancellationRequested)
            {
                var isEvenMinute = DateTime.Now.Minute % 2 == 0;
                Log.Debug("Checking if feature toggle has changed (isOddMinute={isEvenMinute}), current state is {state}", isEvenMinute, _stateMachine.State);
                
                if (isEvenMinute)
                {
                    Log.Information("{state}: Firing trigger: 'Reset'", _stateMachine.State);
                    await _stateMachine.FireAsync(Trigger.Reset);
                    break;
                }
                
                Log.Debug("Sleeping for {sleepSeconds} seconds.", sleepSeconds);
                Thread.Sleep(sleepSeconds * 1000);                    
            }
        }

        private async Task ListenForFeatureToggleToSet(CancellationToken stopToken)
        {
            Log.Information("{state}: Entered: '{method_name}'", _stateMachine.State, nameof(ListenForFeatureToggleToSet).ToString());
            Log.Debug("Listening For feature toggle to set");
            var sleepSeconds = 15;
            while (!stopToken.IsCancellationRequested)
            {
                var isOddMinute = DateTime.Now.Minute % 2 == 1;
                Log.Debug("Checking if feature toggle has changed (isOddMinute={isOddMinute}), current state is {state}", isOddMinute, _stateMachine.State.ToString());
                
                if (isOddMinute)
                {
                    Log.Information("{state}: Firing trigger: 'Set'", _stateMachine.State);
                    await _stateMachine.FireAsync(Trigger.Set);
                    break;
                }
                
                Log.Debug("Sleeping for {sleepSeconds} seconds.", sleepSeconds);
                Thread.Sleep(sleepSeconds * 1000);
            }
        }

        private async Task RunJob(CancellationToken stopToken)
        {
            Log.Information("{state}: Entered: '{method_name}'", _stateMachine.State, nameof(RunJob));
            Thread.Sleep(5_000);
            Log.Information("{state}: Firing trigger: 'JobFinished'", _stateMachine.State);
            await _stateMachine.FireAsync(Trigger.JobFinished);
        }
    }
}