using Fusion;
using UnityEngine;

public class MissionUI : MonoBehaviour
{
    [SerializeField] private MissionSystem missionSystem;
    [SerializeField] private MissionDefinition[] missionDefinitions;

    [SerializeField] private Transform contentParent;
    [SerializeField] private MissionItemUI missionItemPrefab;

    private float refreshTimer;

    private void Update()
    {
        if (!CanRefresh())
            return;

        refreshTimer -= Time.deltaTime;

        if (refreshTimer <= 0f)
        {
            refreshTimer = 0.2f;
            Refresh();
        }
    }

    private bool CanRefresh()
    {
        if (missionSystem == null)
            return false;

        if (missionSystem.Runner == null)
            return false;

        if (contentParent == null)
            return false;

        if (missionItemPrefab == null)
            return false;

        return true;
    }

    private void Refresh()
    {
        if (!CanRefresh())
            return;

        ClearItems();

        PlayerRef localPlayer = missionSystem.Runner.LocalPlayer;

        for (int i = 0; i < missionSystem.MissionCount; i++)
        {
            MissionRuntimeState state = missionSystem.Missions[i];

            if (!ShouldShowMission(state, localPlayer))
                continue;

            MissionDefinition definition = FindMissionDefinition(state.MissionId);

            string missionName = definition != null
                ? definition.missionName
                : $"Unknown Mission {state.MissionId}";

            MissionItemUI item = Instantiate(missionItemPrefab, contentParent, false);

            item.Set(
                missionName,
                state.Progress,
                state.RequiredProgress,
                state.IsCompleted
            );
        }
    }

    private bool ShouldShowMission(MissionRuntimeState state, PlayerRef localPlayer)
    {
        if ((MissionType)state.Type == MissionType.Common)
            return true;

        return state.Owner == localPlayer;
    }

    private MissionDefinition FindMissionDefinition(int missionId)
    {
        foreach (MissionDefinition definition in missionDefinitions)
        {
            if (definition.missionId == missionId)
                return definition;
        }

        return null;
    }

    private void ClearItems()
    {
        for (int i = contentParent.childCount - 1; i >= 0; i--)
        {
            Destroy(contentParent.GetChild(i).gameObject);
        }
    }
}