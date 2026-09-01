using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Item",menuName = "世界物品")]
[Serializable]
public class WorldItem : ScriptableObject
{
    public string Name;
    public string Description;
    public Vector3 pos;
}
