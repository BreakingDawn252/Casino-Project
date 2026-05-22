/* * Canvas Name: BlackjackReset
 * Version: 1
 */
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class BlackjackReset : UdonSharpBehaviour
{
    public BlackjackManager manager;

    public void OnClickResetButton()
    {
        if (manager == null) return;

        if (!Networking.IsOwner(manager.gameObject))
        {
            Networking.SetOwner(Networking.LocalPlayer, manager.gameObject);
        }
        
        manager.ForceResetTable();
    }
}