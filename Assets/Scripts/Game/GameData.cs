using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class EnemyData
{
    public float posX, posY, posZ;
    public bool isAlive;
    public float health;
}

[System.Serializable]
public class SceneProgress
{
    public string sceneName;
    public int candlesCollected;
}

[System.Serializable]
public class GameData
{
    public string sceneName;
    public float posX;
    public float posY;
    public float posZ;

    public List<SceneProgress> sceneProgressList = new List<SceneProgress>();
    public List<EnemyData> enemyList = new List<EnemyData>();

    public Vector3 PlayerPosition => new Vector3(posX, posY, posZ);

    public int GetCandlesCollected(string scene)
    {
        SceneProgress entry = sceneProgressList.Find(s => s.sceneName == scene);
        return entry != null ? entry.candlesCollected : 0;
    }

    public void SetCandlesCollected(string scene, int count)
    {
        SceneProgress entry = sceneProgressList.Find(s => s.sceneName == scene);
        if (entry != null)
            entry.candlesCollected = count;
        else
            sceneProgressList.Add(new SceneProgress { sceneName = scene, candlesCollected = count });
    }

    public EnemyData GetEnemyByPosition(Vector3 pos, float threshold = 1f)
    {
        return enemyList.Find(e =>
            Vector3.Distance(new Vector3(e.posX, e.posY, e.posZ), pos) <= threshold);
    }

    public void SetEnemyData(Vector3 pos, bool isAlive, float health)
    {
        EnemyData entry = GetEnemyByPosition(pos);
        if (entry != null)
        {
            entry.isAlive = isAlive;
            entry.health  = health;
        }
        else
        {
            enemyList.Add(new EnemyData { posX = pos.x, posY = pos.y, posZ = pos.z, isAlive = isAlive, health = health });
        }
    }
}
