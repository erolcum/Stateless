using Stateless;
using Stateless.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ConsoleApp1
{
    class Program
    {
         public enum State
        {
            Draft,
            Review,
            ChangesRequested,
            SubmittedToClient,
            Approved,
            Declined
        }

        private enum Triggers
        {
            UpdateDocument,
            BeginReview,
            ChangedNeeded,
            Accept,
            Reject,
            Submit,
            Decline,
            RestartReview,
            Approve,
        }
        private readonly StateMachine<State, Triggers> machine;

        private readonly StateMachine<State, Triggers>.TriggerWithParameters<string> changedNeededParameters;
        public Program()
        {
            machine = new StateMachine<State, Triggers>(State.Draft);

            machine.Configure(State.Draft)
                .PermitReentry(Triggers.UpdateDocument)
                .Permit(Triggers.BeginReview, State.Review)
                .OnEntryAsync(OnDraftEntryAsync)
                .OnExitAsync(OnDraftExitAsync);

            changedNeededParameters = machine.SetTriggerParameters<string>(Triggers.ChangedNeeded);

            machine.Configure(State.Review)
                .Permit(Triggers.ChangedNeeded, State.ChangesRequested)
                .Permit(Triggers.Submit, State.SubmittedToClient)
                .OnEntryAsync(OnReviewEntryAsync)
                .OnExitAsync(OnReviewExitAsync);

            machine.Configure(State.ChangesRequested)
                .Permit(Triggers.Reject, State.Review)
                .Permit(Triggers.Accept, State.Draft)
                .OnEntryAsync(OnChangesRequestedEntryAsync)
                .OnExitAsync(OnChangesRequestedExitAsync);

            machine.Configure(State.SubmittedToClient)
                .Permit(Triggers.Approve, State.Approved)
                .Permit(Triggers.Decline, State.Declined)
                .OnEntryAsync(OnSubmittedToClientEnterAsync)
                .OnExitAsync(OnSubmittedToClientExitAsync);

            machine.Configure(State.Declined)
                .Permit(Triggers.RestartReview, State.Review)
                .OnEntryAsync(OnDeclinedEnterAsync)
                .OnExitAsync(OnDeclinedExitAsync);

            machine.Configure(State.Approved)
                .OnEntryAsync(OnApprovedEnter);
        }

        private async Task OnDraftEntryAsync()
        {
            await notificationService("The proposal is now in the draft stage");

        }
        private async Task OnDraftExitAsync()
        {
            await notificationService("The proposal has now left the draft stage");
        }

        private async Task OnReviewEntryAsync()
        {
            await notificationService("The proposal is now in the review stage");
        }

        private async Task OnReviewExitAsync()
        {
            await notificationService("The proposal has now left the review stage");
        }
        private async Task OnChangesRequestedEntryAsync()
        {
            await notificationService("The proposal is now in change requested stage");
        }
        private async Task OnChangesRequestedExitAsync()
        {
            await notificationService("The proposal has now left change requested stage");
        }
        private async Task OnSubmittedToClientEnterAsync()
        {
            await notificationService("The proposal is now in submitted to client stage");
        }
        private async Task OnSubmittedToClientExitAsync()
        {
            await notificationService("The proposal has now left the submitted to client stage");
        }
        private async Task OnDeclinedEnterAsync()
        {
            await notificationService("The proposal is now in declined stage");
        }
        private async Task OnDeclinedExitAsync()
        {
            await notificationService("The proposal has now left the declined stage");
        }
        private async Task OnApprovedEnter()
        {
            await notificationService("The proposal is now in the approved stage");

        }

    
        public async Task notificationService(string str) 
        {
            await Console.Out.WriteLineAsync(str);      
        }

        public State CurrentState() 
        {
            return machine.State; 
        }
        public string GetGraph()
        {
            return UmlDotGraph.Format(machine.GetInfo());
        }

        public async Task UpdateDocumentAsync() => await machine.FireAsync(Triggers.UpdateDocument);

        public async Task BeginReviewAsync() => await machine.FireAsync(Triggers.BeginReview);

        public async Task MakeChangeAsync(string change) => await machine.FireAsync(changedNeededParameters, change);

        public async Task AcceptAsync() => await machine.FireAsync(Triggers.Accept);

        public async Task RejectAsync() => await machine.FireAsync(Triggers.Reject);

        public async Task SubmitAsync() => await machine.FireAsync(Triggers.Submit);

        public async Task RestartReviewAsync() => await machine.FireAsync(Triggers.RestartReview);

        public async Task ApproveAsync() => await machine.FireAsync(Triggers.Approve);

        public async Task DeclineAsync() => await machine.FireAsync(Triggers.Decline);

       
    }

    class Yuru 
    {
        static async Task Main(string[] args)
        {
            var document = new Program();
            Console.WriteLine(document.CurrentState());
            await document.BeginReviewAsync();
            Console.WriteLine(document.CurrentState());
            await document.MakeChangeAsync("hello");
            Console.WriteLine(document.CurrentState());
            await document.RejectAsync();
            Console.WriteLine(document.CurrentState());
            await document.SubmitAsync();
            Console.WriteLine(document.CurrentState());
            
            await document.ApproveAsync();
            Console.WriteLine(document.CurrentState());
            Console.WriteLine();
            Console.WriteLine();
            string graph = document.GetGraph();
            Console.WriteLine(graph);   


            Console.ReadLine();
        }

    }
}
