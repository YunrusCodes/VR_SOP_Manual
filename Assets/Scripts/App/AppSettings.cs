using UnityEngine;

namespace Inspection.App
{
    [CreateAssetMenu(fileName = "AppSettings", menuName = "Inspection/AppSettings")]
    public sealed class AppSettings : ScriptableObject
    {
        [Tooltip("Base URL of the FastAPI backend. Use the dev machine's LAN IP for Quest 3 testing.")]
        public string ApiBaseUrl = "http://192.168.1.10:8000";

        [Tooltip("Company namespace under storage/. Hard-coded in MVP.")]
        public string Company = "acme";

        [Tooltip("If on, Logger.Verbose() messages are emitted to Console.")]
        public bool VerboseLog = true;
    }
}
