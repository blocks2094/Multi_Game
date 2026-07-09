using Unity.VisualScripting;
using UnityEngine;

public enum MissionType
{
    Common,     // 공통미션
    Personal    // 개인미션
}

// 미션 원본 데이터
[CreateAssetMenu(menuName = "Mission/Mission Definition")]
public class MissionDefinition : ScriptableObject
{
    public int missionId;               
    public string missionName;          
    public MissionType missionType;     
    public int requiredProgress = 1;    
}
