using System;
using UnityEngine;

[Serializable]
public class Stats
{
    public int maxHp = 10;
    public int attack = 10;
    public int defense;
    public float power;

    public int ScaledAttack => Mathf.Max(1, Mathf.RoundToInt(attack * (1f + power / 100f)));

    public int Reduce(int damage) => Mathf.Max(1, damage - defense);
}
