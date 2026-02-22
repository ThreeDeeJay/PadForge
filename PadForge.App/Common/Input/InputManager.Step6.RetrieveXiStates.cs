using System;
using PadForge.Engine;

namespace PadForge.Common.Input
{
    public partial class InputManager
    {
        // ─────────────────────────────────────────────
        //  Step 6: RetrieveXiStates
        //  Reads back the XInput states from the system's XInput DLL.
        //  This reflects what the game actually sees from the virtual controllers
        //  (or from native XInput controllers). Used by the UI to display the
        //  final output state after all processing.
        // ─────────────────────────────────────────────

        /// <summary>
        /// Step 6: For each of the 4 controller slots, reads the current XInput
        /// state via the system's xinput1_4.dll. The result is stored in
        /// <see cref="RetrievedXiStates"/> for UI display.
        /// 
        /// This reads what the game would see if it called XInputGetState().
        /// If ViGEmBus is active, this reflects the virtual controller state.
        /// If no virtual controller exists for a slot, this reflects the native
        /// Xbox controller (if present) or returns a zeroed state.
        /// </summary>
        private void RetrieveXiStates()
        {
            for (int padIndex = 0; padIndex < MaxPads; padIndex++)
            {
                try
                {
                    if (XInputInterop.GetStateEx(padIndex, out XInputState state))
                    {
                        RetrievedXiStates[padIndex] = state.Gamepad;
                    }
                    else
                    {
                        // Controller not connected at this slot.
                        RetrievedXiStates[padIndex].Clear();
                    }
                }
                catch (Exception ex)
                {
                    RaiseError($"Error retrieving XInput state for pad {padIndex}", ex);
                    RetrievedXiStates[padIndex].Clear();
                }
            }
        }
    }
}
