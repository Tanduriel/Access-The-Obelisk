using HarmonyLib;

namespace AccessTheObelisk
{
    /// <summary>
    /// Guards native game activation paths immediately after modal alerts close.
    /// </summary>
    internal static class ModalInputGuardPatch
    {
        [HarmonyPatch(typeof(AlertManager), nameof(AlertManager.SetConfirmAnswer))]
        internal static class AlertManagerSetConfirmAnswerPatch
        {
            private static void Postfix()
            {
                InputActivationGuard.BlockSubmitAfterModal();
            }
        }

        [HarmonyPatch(typeof(AlertManager), nameof(AlertManager.AlertInputSuccess))]
        internal static class AlertManagerAlertInputSuccessPatch
        {
            private static void Postfix()
            {
                InputActivationGuard.BlockSubmitAfterModal();
            }
        }

        [HarmonyPatch(typeof(BotonGeneric), nameof(BotonGeneric.OnMouseUp))]
        internal static class BotonGenericOnMouseUpPatch
        {
            private static bool Prefix()
            {
                return !InputActivationGuard.ShouldBlockSubmit();
            }
        }

        [HarmonyPatch(typeof(BotonGeneric), nameof(BotonGeneric.Clicked))]
        internal static class BotonGenericClickedPatch
        {
            private static bool Prefix()
            {
                return !InputActivationGuard.ShouldBlockSubmit();
            }
        }

        [HarmonyPatch(typeof(TownManager), nameof(TownManager.Ready))]
        internal static class TownManagerReadyPatch
        {
            private static bool Prefix()
            {
                return !InputActivationGuard.ShouldBlockSubmit();
            }
        }
    }
}
