using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Procedural Worlds/Gaia/Water Settings")]
public class GRC_WaterSettings : ScriptableObject
{
    public GameObject m_builtInWaterPrefab;
    public GameObject m_URPWaterPrefab;
    public GameObject m_HDRPWaterPrefab;
}
