using ConsoleApp1;
using Stateless;
using Stateless.Graph;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ConsoleApp1
{
    public enum AlarmCommand
    {
        Startup,
        Arm,
        Disarm,
        Trigger,
        Acknowledge,
        Pause,
        TimeOut
    }
    public enum AlarmState
    {
        Undefined,
        Disarmed,
        Prearmed,
        Armed,
        Triggered,
        ArmPaused,
        PreTriggered,
        Acknowledged
    }
    /// <summary>
    /// A sample class that implements an alarm as a state machine using Stateless 
    /// (https://github.com/dotnet-state-machine/stateless).
    /// 
    /// It also shows one way that temporary states can be implemented with the use of 
    /// Timers. PreArmed, PreTriggered, Triggered, and ArmPaused are "temporary" states with
    /// a configurable delay (i.e. to allow for an "arm delay"... a delay between Disarmed
    /// and Armed). The Triggered state is also considered temporary, since if an alarm 
    /// sounds for a certain period of time and no-one Acknowledges it, the state machine
    /// returns to the Armed state.
    /// 
    /// Timers are triggered via OnEntry() and OnExit() methods. Transitions are written to
    /// the Trace in order to show what happens.
    /// 
    /// The included PNG file shows what the state flow looks like.
    /// 
    /// </summary>
    class Alarm
    {
        /// <summary>
        /// Moves the Alarm into the provided <see cref="AlarmState" /> via the defined <see cref="AlarmCommand" />.
        /// </summary>
        /// <param name="command">The <see cref="AlarmCommand" /> to execute on the current <see cref="AlarmState" />.</param>
        /// <returns>The new <see cref="AlarmState" />.</returns>
        public AlarmState ExecuteTransition(AlarmCommand command)
        {
            if (_machine.CanFire(command))
            {
                _machine.Fire(command);
            }
            else
            {
                throw new InvalidOperationException($"Cannot transition from {CurrentState()} via {command}");
            }

            return CurrentState();
        }

        /// <summary>
        /// The current <see cref="AlarmState" /> of the alarm.
        /// </summary>
        public AlarmState CurrentState()
        {
            if (_machine != null)
                return _machine.State;
            else
                throw new InvalidOperationException("Alarm hasn't been configured yet.");
        }

        /// <summary>
        /// Defines whether the <see cref="Alarm"/> has been configured.
        /// </summary>
        public bool IsConfigured { get; private set; }

        /// <summary>
        /// Returns whether the provided command is a valid transition from the Current State.
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public bool CanFireCommand(AlarmCommand command)
        {
            return _machine.CanFire(command);
        }

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="armDelay">The time (in seconds) the alarm will spend in the
        /// Prearmed status before continuing to the Armed status (if not transitioned to
        /// Disarmed via Disarm).</param>
        /// <param name="pauseDelay">The time (in seconds) the alarm will spend in the 
        /// ArmPaused status before returning to Armed (if not transitioned to Triggered 
        /// via Trigger).</param>
        /// <param name="triggerDelay">The time (in seconds) the alarm will spend in the
        /// PreTriggered status before continuing to the Triggered status (if not 
        /// transitioned to Disarmed via Disarm).</param>
        /// <param name="triggerTimeOut">The time (in seconds) the alarm will spend in the
        /// Triggered status before returning to the Armed status (if not transitioned to
        /// Disarmed via Disarm).</param>
        public Alarm(int armDelay, int pauseDelay, int triggerDelay, int triggerTimeOut)
        {
            _machine = new StateMachine<AlarmState, AlarmCommand>(AlarmState.Undefined);

            preArmTimer = new System.Timers.Timer(armDelay * 1000) { AutoReset = false, Enabled = false };
            preArmTimer.Elapsed += TimeoutTimerElapsed;
            pauseTimer = new System.Timers.Timer(pauseDelay * 1000) { AutoReset = false, Enabled = false };
            pauseTimer.Elapsed += TimeoutTimerElapsed;
            triggerDelayTimer = new System.Timers.Timer(triggerDelay * 1000) { AutoReset = false, Enabled = false };
            triggerDelayTimer.Elapsed += TimeoutTimerElapsed;
            triggerTimeOutTimer = new System.Timers.Timer(triggerTimeOut * 1000) { AutoReset = false, Enabled = false };
            triggerTimeOutTimer.Elapsed += TimeoutTimerElapsed;

            _machine.OnTransitioned(OnTransition);

            _machine.Configure(AlarmState.Undefined)
                .Permit(AlarmCommand.Startup, AlarmState.Disarmed)
                .OnExit(() => IsConfigured = true);

            _machine.Configure(AlarmState.Disarmed)
                .Permit(AlarmCommand.Arm, AlarmState.Prearmed);

            _machine.Configure(AlarmState.Armed)
                .Permit(AlarmCommand.Disarm, AlarmState.Disarmed)
                .Permit(AlarmCommand.Trigger, AlarmState.PreTriggered)
                .Permit(AlarmCommand.Pause, AlarmState.ArmPaused);

            _machine.Configure(AlarmState.Prearmed)
                .OnEntry(() => ConfigureTimer(true, preArmTimer, "Pre-arm"))
                .OnExit(() => ConfigureTimer(false, preArmTimer, "Pre-arm"))
                .Permit(AlarmCommand.TimeOut, AlarmState.Armed)
                .Permit(AlarmCommand.Disarm, AlarmState.Disarmed);

            _machine.Configure(AlarmState.ArmPaused)
                .OnEntry(() => ConfigureTimer(true, pauseTimer, "Pause delay"))
                .OnExit(() => ConfigureTimer(false, pauseTimer, "Pause delay"))
                .Permit(AlarmCommand.TimeOut, AlarmState.Armed)
                .Permit(AlarmCommand.Trigger, AlarmState.PreTriggered);

            _machine.Configure(AlarmState.Triggered)
                .OnEntry(() => ConfigureTimer(true, triggerTimeOutTimer, "Trigger timeout"))
                .OnExit(() => ConfigureTimer(false, triggerTimeOutTimer, "Trigger timeout"))
                .Permit(AlarmCommand.TimeOut, AlarmState.Armed)
                .Permit(AlarmCommand.Acknowledge, AlarmState.Acknowledged);

            _machine.Configure(AlarmState.PreTriggered)
                .OnEntry(() => ConfigureTimer(true, triggerDelayTimer, "Trigger delay"))
                .OnExit(() => ConfigureTimer(false, triggerDelayTimer, "Trigger delay"))
                .Permit(AlarmCommand.TimeOut, AlarmState.Triggered)
                .Permit(AlarmCommand.Disarm, AlarmState.Disarmed);

            _machine.Configure(AlarmState.Acknowledged)
                .Permit(AlarmCommand.Disarm, AlarmState.Disarmed);

            _machine.Fire(AlarmCommand.Startup);
        }

        private void TimeoutTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _machine.Fire(AlarmCommand.TimeOut);
        }

        private void ConfigureTimer(bool active, System.Timers.Timer timer, string timerName)
        {
            if (timer != null)
                if (active)
                {
                    timer.Start();
                    Trace.WriteLine($"{timerName} started.");
                }
                else
                {
                    timer.Stop();
                    Trace.WriteLine($"{timerName} cancelled.");
                }
        }

        private void OnTransition(StateMachine<AlarmState, AlarmCommand>.Transition transition)
        {
            Trace.WriteLine($"Transitioned from {transition.Source} to " +
                $"{transition.Destination} via {transition.Trigger}.");
        }

        private StateMachine<AlarmState, AlarmCommand> _machine;
        private System.Timers.Timer preArmTimer;
        private System.Timers.Timer pauseTimer;
        private System.Timers.Timer triggerDelayTimer;
        private System.Timers.Timer triggerTimeOutTimer;
    }

    class Test 
    {
        static Alarm _alarm;

        static void Main(string[] args)
        {
            _alarm = new Alarm(10, 10, 10, 10);

            string input = "";

            WriteHeader();

            while (input != "q")
            {
                Console.Write("> ");

                input = Console.ReadLine();

                if (!string.IsNullOrWhiteSpace(input))
                    switch (input.Split(' ')[0])
                    {
                        case "q":
                            Console.WriteLine("Exiting...");
                            break;
                        case "fire":
                            WriteFire(input);
                            break;
                        case "canfire":
                            WriteCanFire();
                            break;
                        case "state":
                            WriteState();
                            break;
                        case "h":
                        case "help":
                            WriteHelp();
                            break;
                        case "c":
                        case "clear":
                            Console.Clear();
                            WriteHeader();
                            break;
                        default:
                            Console.WriteLine("Invalid command. Type 'h' or 'help' for valid commands.");
                            break;
                    }
            }
        }

        static void WriteHelp()
        {
            Console.WriteLine("Valid commands:");
            Console.WriteLine("q               - Exit");
            Console.WriteLine("fire <state>    - Tries to fire the provided commands");
            Console.WriteLine("canfire <state> - Returns a list of fireable commands");
            Console.WriteLine("state           - Returns the current state");
            Console.WriteLine("c / clear       - Clear the window");
            Console.WriteLine("h / help        - Show this again");
        }

        static void WriteHeader()
        {
            Console.WriteLine("Stateless-based alarm test application:");
            Console.WriteLine("---------------------------------------");
            Console.WriteLine("");
        }

        static void WriteCanFire()
        {
            foreach (AlarmCommand command in (AlarmCommand[])Enum.GetValues(typeof(AlarmCommand)))
                if (_alarm != null && _alarm.CanFireCommand(command))
                    Console.WriteLine($"{Enum.GetName(typeof(AlarmCommand), command)}");
        }

        static void WriteState()
        {
            if (_alarm != null)
                Console.WriteLine($"The current state is {Enum.GetName(typeof(AlarmState), _alarm.CurrentState())}");
        }

        static void WriteFire(string input)
        {
            if (input.Split(' ').Length == 2)
            {
                try
                {
                    if (Enum.TryParse(input.Split(' ')[1], out AlarmCommand command))
                    {
                        if (_alarm != null)
                            _alarm.ExecuteTransition(command);
                    }
                    else
                    {
                        Console.WriteLine($"{input.Split(' ')[1]} is not a valid AlarmCommand.");
                    }
                }
                catch (InvalidOperationException ex)
                {
                    Console.WriteLine($"{input.Split(' ')[1]} is not a valid AlarmCommand to the current state.");
                }
            }
            else
            {
                Console.WriteLine("fire requires you to specify the command you want to fire.");
            }
        }
    }
}

