using System;
using PadForge.Engine;

namespace PadForge.Common.Input
{
    public partial class InputManager
    {
        // ─────────────────────────────────────────────
        //  Step 6: RetrieveXiStates
        //  Reads back the XInput states from the system's XInput DLL.
        //  This reflects what the game actually sees from the virtual
        //  controllers (ViGEmBus). Used by the UI to display the final
        //  output state after all processing.
        //
        //  P/Invoke: reuses XInputGetStateEx declared in Step5.VirtualDevices.cs
        // ─────────────────────────────────────────────

        /// <summary>
        /// Step 6: For each of the 4 controller slots, reads the current XInput
        /// state via the system's xinput1_4.dll. The result is stored in
        /// <see cref="RetrievedXiStates"/> for UI display.
        ///
        /// This reads what the game would see if it called XInputGetState().
        /// If ViGEmBus is active, this reflects the virtual controller state.
        /// </summary>
        private void RetrieveXiStates()
        {
            for (int padIndex = 0; padIndex < MaxPads; padIndex++)
            {
                try
                {
                    var nativeState = new XInputStateInternal();
                    uint result = XInputGetStateEx((uint)padIndex, ref nativeState);

                    if (result != XINPUT_ERROR_DEVICE_NOT_CONNECTED)
                    {
                        // Convert the internal struct to the engine's Gamepad struct.
                        RetrievedXiStates[padIndex].Buttons = nativeState.Gamepad.wButtons;
                        RetrievedXiStates[padIndex].LeftTrigger = nativeState.Gamepad.bLeftTrigger;
                        RetrievedXiStates[padIndex].RightTrigger = nativeState.Gamepad.bRightTrigger;
                        RetrievedXiStates[padIndex].ThumbLX = nativeState.Gamepad.sThumbLX;
                        RetrievedXiStates[padIndex].ThumbLY = nativeState.Gamepad.sThumbLY;
                        RetrievedXiStates[padIndex].ThumbRX = nativeState.Gamepad.sThumbRX;
                        RetrievedXiStates[padIndex].ThumbRY = nativeState.Gamepad.sThumbRY;
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
