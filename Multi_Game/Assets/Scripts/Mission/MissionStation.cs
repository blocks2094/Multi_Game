using UnityEngine;

public class MissionStation : MonoBehaviour
{
    [SerializeField] private int missionId;

    private MissionSystem missionSystem;

    private void Awake()
    {
        missionSystem = FindAnyObjectByType<MissionSystem>();
    }

    public void Interact()
    {
        if (missionSystem == null)
            return;

        missionSystem.RPC_RequestMissionProgress(missionId);
    }
}