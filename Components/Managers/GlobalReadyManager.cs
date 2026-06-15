using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace TTS_Company.Components.Managers
{
    internal class GlobalReadyManager : NetworkBehaviour
    {

        // later, rework this code

        //public static GlobalReadyManager Instance { get; private set; }

        //private Dictionary<string, HashSet<ulong>> readyStates = new Dictionary<string, HashSet<ulong>>();

        //private void Awake()
        //{
        //    if (Instance == null) Instance = this;
        //    else Destroy(gameObject);
        //}

        //public void StartGlobalFunctionSequence(string readyIdentifier)
        //{
        //    if (!IsServer) return;

        //    if (readyStates.ContainsKey(readyIdentifier))
        //    {
        //        readyStates[readyIdentifier].Clear();
        //    }

        //    RunFunctionOnAllClientsClientRpc(readyIdentifier);
        //}

        //[ClientRpc]
        //private void RunFunctionOnAllClientsClientRpc(string readyIdentifier)
        //{
        //    // Every single player (including the host) executes this locally
        //    ExecuteLocalWork(readyIdentifier);
        //}

        ///// <summary>
        ///// Function called from anywhere (or via UI/Trigger) to change a specific client's ready status.
        ///// Safely routes through a ServerRpc so clients can call it.
        ///// </summary>
        //public void SetClientReadyStatus(string readyIdentifier, ulong clientId)
        //{
        //    // Route to server execution
        //    SetClientReadyStatusServerRpc(readyIdentifier, clientId);
        //}

        //[ServerRpc(RequireOwnership = false)]
        //private void SetClientReadyStatusServerRpc(string readyIdentifier, ulong clientId)
        //{
        //    if (!IsServer) return;

        //    // Initialize the tracking set for this specific identifier if it doesn't exist yet
        //    if (!readyStates.ContainsKey(readyIdentifier))
        //    {
        //        readyStates[readyIdentifier] = new HashSet<ulong>();
        //    }

        //    readyStates[readyIdentifier].Add(clientId);

        //    CheckReadyState(readyIdentifier);
        //}

        //private void CheckReadyState(string readyIdentifier)
        //{
        //    if (!IsServer) return;

        //    // 1. Gather all currently active connected clients right now
        //    // Lethal Company tracks active controller scripts in the allPlayerScripts array
        //    List<ulong> activeClientIds = new List<ulong>();

        //    foreach (var playerScript in StartOfRound.Instance.allPlayerScripts)
        //    {
        //        if (playerScript != null && playerScript.isPlayerControlled)
        //        {
        //            activeClientIds.Add(playerScript.actualClientId);
        //        }
        //    }

        //    int requiredCount = activeClientIds.Count;

        //    // Clean up our ready set just in case someone disconnected while readied up
        //    readyStates[readyIdentifier].IntersectWith(activeClientIds);

        //    // 2. Check if everyone in-game is currently ready for this specific identifier
        //    if (readyStates[readyIdentifier].Count >= requiredCount && requiredCount > 0)
        //    {
        //        // Convert the current list of in-game clients to an array snapshot
        //        ulong[] inGameClientsSnapshot = activeClientIds.ToArray();

        //        // 3. Clear state before firing to prevent accidental double-triggers
        //        readyStates[readyIdentifier].Clear();

        //        // 4. Trigger the final function
        //        OnAllPlayersReady(readyIdentifier, inGameClientsSnapshot);
        //    }
        //}

        ///// <summary>
        ///// The master function that fires ONLY on the host when every player is ready.
        ///// </summary>
        ///// <param name="readyIdentifier">The unique string key of the event that just filled up.</param>
        ///// <param name="clientsInGame">Snapshot array of all client IDs connected at the exact moment it triggered.</param>
        //private void OnAllPlayersReady(string readyIdentifier, ulong[] clientsInGame)
        //{
        //    Debug.Log($"[ReadyMod] Event '{readyIdentifier}' successfully triggered! Total players snapshot: {clientsInGame.Length}");

        //    // Handle your logic depending on the string identifier
        //    switch (readyIdentifier)
        //    {
        //        case "start_boss_fight":
        //            // Handle boss initiation logic here...
        //            break;

        //        case "skip_orbit_timer":
        //            // Handle ship routing logic here...
        //            break;

        //        default:
        //            Debug.LogWarning($"[ReadyMod] Received unhandled ready identifier: {readyIdentifier}");
        //            break;
        //    }

        //    // If clients need to execute visual or local changes, broadcast to them from here:
        //    // DispatchReadyEventClientRpc(readyIdentifier, clientsInGame);
        //}

        //private void ExecuteLocalWork(string readyIdentifier)
        //{
        //    ulong localClientId = NetworkManager.Singleton.LocalClientId;
        //    Debug.Log($"[ReadyMod] Starting local work for: {readyIdentifier}");

        //    // Example A: Your function is instantaneous (like a UI animation fade)
        //    if (readyIdentifier == "fast_fade_effect")
        //    {
        //        // Do your instant work here...

        //        // Immediately report ready
        //        SetClientReadyStatus(readyIdentifier, localClientId, true);
        //    }

        //    // Example B: Your function takes time (like loading an asset or a long animation)
        //    else if (readyIdentifier == "complex_map_generation")
        //    {
        //        // Start a coroutine to handle async/timed work
        //        StartCoroutine(DoTimedWorkCoroutine(readyIdentifier, localClientId));
        //    }
        //}

        //private IEnumerator DoTimedWorkCoroutine(string readyIdentifier, ulong localClientId)
        //{
        //    // 1. Simulate your custom local process (e.g., waiting for an asset to load, fading screen)
        //    // Yielding for 3 seconds as an example
        //    yield return new WaitForSeconds(3.0f);

        //    Debug.Log($"[ReadyMod] Local work finished for {readyIdentifier}. Reporting ready to host.");

        //    // 2. The work is officially finished locally! Tell the host we are ready.
        //    SetClientReadyStatus(readyIdentifier, localClientId, true);
        //}
    }
}
