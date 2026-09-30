using System;
using UnityEngine;

[Serializable]
public class Stats
{
    public int maxHp = 10;
    public int attack = 10;
    public int defense;

    public int Reduce(int damage) => Mathf.Max(1, damage - defense);
}
