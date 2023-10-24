using ConsoleApp1;
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
    class LightSwitch
    {
        TimeSpan _time;
        bool _enforceTimeConstraint;
        string _name;

        public State CurrentState = State.OFF;
        public enum State { ON, OFF };
        enum Trigger { TOGGLE };
        StateMachine<LightSwitch.State, Trigger> _machine;
        public LightSwitch(string name, bool initialState = false, bool enableTimeConstraint = false)
        {
            if (initialState) CurrentState = State.ON;
            _enforceTimeConstraint = enableTimeConstraint;
            _time = new TimeSpan(0, 0, 0);

            _machine = new StateMachine<State, Trigger>(() => CurrentState, s => CurrentState = s);

            _machine.Configure(State.ON)
                    .Permit(Trigger.TOGGLE, State.OFF);

            _machine.Configure(State.OFF)
                    .PermitIf(Trigger.TOGGLE, State.ON, () => IsLightNeeded(), "Toggle allowed")
                    .PermitReentryIf(Trigger.TOGGLE, () => !IsLightNeeded(), "Toggle not allowed");
            Console.WriteLine("--------------------------------------");
            Console.WriteLine(name + " switch initial state : " + CurrentState.ToString());
            Console.WriteLine("Enable Time Constraint : " + enableTimeConstraint.ToString());
        }

        private TimeSpan GetCurrentTime()
        {
            if (_time == new TimeSpan(0, 0, 0))
            {
                return DateTime.Now.TimeOfDay;
            }
            else
            {
                return _time;
            }
        }

        private bool IsDaylight(TimeSpan time)
        {
            if (time.Hours >= 6 && time.Hours <= 18)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void SetTimeOfDay(TimeSpan time)
        {
            _time = time;

        }

        public void ToggleSwitch()
        {
            _machine.Fire(Trigger.TOGGLE);

            Console.WriteLine("Switch is " + CurrentState.ToString());
        }

        bool IsLightNeeded()
        {
            bool result = false;
            if (_enforceTimeConstraint)
            {
                result = !IsDaylight(GetCurrentTime());
            }
            else result = true;

            return result;
        }
        public string ShowStateMachine()
        {
            return UmlDotGraph.Format(_machine.GetInfo());
        }
    }

    class Test 
    {
        static void Main(string[] args)
        {
            LightSwitch stateSwitch = new LightSwitch("Stateless Switch", true, true);
            Console.WriteLine("\n Toggling stateless switch at predefined time...\n");
            StartSwitchAtDefinedTime(stateSwitch);
            //Console.WriteLine("Graph >>>>>>>>>>>.");
            //Console.WriteLine(stateSwitch.ShowStateMachine());


            Console.ReadLine();
        }

        static void StartSwitchAtDefinedTime(LightSwitch mySwitch)
        {
            TimeSpan[] times =
            {
                new TimeSpan(4,30,30),
                new TimeSpan(9,45,0),
                new TimeSpan(14,2,6),
                new TimeSpan(19,21,34),
                new TimeSpan(23,46,0),
                new TimeSpan(23,55,0)
            };

            foreach (var time in times)
            {
                Console.WriteLine("Toggling switch at " + time.ToString());
                mySwitch.SetTimeOfDay(time);
                mySwitch.ToggleSwitch();
            }

            Console.WriteLine("Toggling switch now " + (DateTime.Now.TimeOfDay));
            mySwitch.SetTimeOfDay(DateTime.Now.TimeOfDay);
            mySwitch.ToggleSwitch();
            
        }

    }
}
