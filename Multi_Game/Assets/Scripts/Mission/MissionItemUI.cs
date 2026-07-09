using TMPro;
using UnityEngine;

public class MissionItemUI : MonoBehaviour
{
    [SerializeField] private TMP_Text missionNameText;
    [SerializeField] private TMP_Text progressText;

    public void Set(string missionName, int progress, int requiredProgress, bool isCompleted)
    {
        missionNameText.text = missionName;

        if (isCompleted)
        {
            progressText.text = "완료";
        }
        else
        {
            progressText.text = $"{progress} / {requiredProgress}";
        }
    }
}