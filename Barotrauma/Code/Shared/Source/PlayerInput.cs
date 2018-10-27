using Microsoft.Xna.Framework.Input;

namespace Barotrauma
{

    public enum InputType 
    { 
        Select, 
        Use,
        Aim,
        Up, Down, Left, Right,
        Attack,
        Run, Crouch,
        Chat, RadioChat, CrewOrders,
        Ragdoll, Health, Grab,
        SelectNextCharacter,
        SelectPreviousCharacter
    }

    public class KeyOrMouse
    {
        Keys keyBinding;
        int? mouseButton;

        public Keys Key
        {
            get { return keyBinding; }
        }
        public int? MouseButton
        {
            get { return mouseButton; }
        }

        public KeyOrMouse(Keys keyBinding)
        {
            this.keyBinding = keyBinding;
        }

        public KeyOrMouse(int mouseButton)
        {
            this.mouseButton = mouseButton;
        }

        public bool IsDown()
        {
            switch (mouseButton)
            {
                case null:
                    return PlayerInput.KeyDown(keyBinding);
                case 0:
                    return PlayerInput.LeftButtonHeld();
                case 1:
                    return PlayerInput.RightButtonHeld();
                case 2:
                    return PlayerInput.MidButtonHeld();
            }
            
            return false;
        }

        public bool IsHit()
        {
            switch (mouseButton)
            {
                case null:
                    return PlayerInput.KeyHit(keyBinding);
                case 0:
                    return PlayerInput.LeftButtonClicked();
                case 1:
                    return PlayerInput.RightButtonClicked();
                case 2:
                    return PlayerInput.MidButtonClicked();
            }

            return false;
        }

        public override string ToString()
        {
            switch (mouseButton)
            {
                case null:
                    return keyBinding.ToString();
                case 0:
                    return "Mouse1";
                case 1:
                    return "Mouse2";
                case 2:
                    return "Mouse3";
            }

            return "None";
        }
    }

    class Key
    {
        private bool hit, hitQueue;
        private bool held, heldQueue;
        
#if CLIENT
        private InputType inputType;

        public Key(InputType inputType)
        {
            this.inputType = inputType;
        }

        private KeyOrMouse binding
        {
            get { return GameMain.Config.KeyBind(inputType); }
        }        
#else
        private KeyOrMouse binding;

        public Key(KeyOrMouse binding)
        {
            this.binding = binding;
        }
#endif

        public KeyOrMouse State
        {
            get { return binding; }
        }

        public bool Hit
        {
            get 
            {
                return hit; 
            }
            set
            {
                hit = value;
            }
        }

        public bool Held
        {
            get
            {
                return held;
            }
            set
            {
                held = value;
            }
        }
        
        public void SetState()
        {
            hit = binding.IsHit();
            if (hit) hitQueue = true;

            held = binding.IsDown();
            if (held) heldQueue = true;
        }

        public void SetState(bool hit, bool held)
        {
            if (hit) hitQueue = true;
            if (held) heldQueue = true;
        }

        public bool DequeueHit()
        {
            bool value = hitQueue;
            hitQueue = false;
            return value;            
        }

        public bool DequeueHeld()
        {
            bool value = heldQueue;
            heldQueue = false;
            return value;            
        }

        public bool GetHeldQueue
        {
            get { return heldQueue; }
        }

        public bool GetHitQueue
        {
            get { return hitQueue; }
        }

        public void Reset()
        {
            hit = false;
            held = false;
        }

        public void ResetHit()
        {
            hit = false;
            //stateQueue = false;
        }

        public void ResetHeld()
        {
            held = false;
            //stateQueue = false;
        }
    }
}
