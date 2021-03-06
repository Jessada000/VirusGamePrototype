﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class BuildManager : SingletonComponent<BuildManager>
{
    /// Contains all of Player's Buildings.
    [SerializeField] List<Builder> allBuildings;

    public List<Builder> AllBuildings { get { return allBuildings; } }

    #region Unity Functions
    protected override void Awake()
    {
        base.Awake();

    }
    protected override void OnInitialize()
    {
    }
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnLevelFinishedLoading;

    }
    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnLevelFinishedLoading;

    }
    void OnLevelFinishedLoading(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "MainScene" && secondCalled)
        {
            Awake();
            Start();
        }
        secondCalled = true;

    }
    void Start()
    {

    }
    #endregion

    public bool CreateNewBuilding(Building.BuildingType type, int teamNumber, Vector2 position)
    {
        Builder laborCenter = allBuildings.SingleOrDefault(b => b.Type == Building.BuildingType.LaborCenter);
        if ((laborCenter == null || laborCenter.Level == 0) && gameObject.transform.Find("TimerCanvas").childCount >= 1)
        {
            Debug.LogWarning("Please Create LaborCenter to perform more Building task !");
            return false;

        }

        if (!ConsumeBuildingCost(new Builder(type)))
        {
            Debug.LogWarning("Upgrade failed : Not enough Resources for upgrading");
            return false;
        }

        GameObject builderGO = Instantiate(Resources.Load<GameObject>("Prefabs/BuildingPrefab"), position, Quaternion.identity);
        builderGO.SetActive(false);
        builderGO.transform.SetParent(this.gameObject.transform.Find("AllBuildings"));

        Builder builder = new Builder(type, position, builderGO);
        Building buildData = LoadManager.Instance.allBuildingData[builder.Type];

        builder.InitializeData();
        builder.constructionStatus = new Builder.Construction { teamNumber = teamNumber, constructPointRequired = buildData.upgradePoint[builder.Level], isConstructing = true };
        AddBuildingsToList(builder);

        builderGO.GetComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>(buildData.spritePath[builder.Level]);
        builderGO.name = builder.ID.ToString();
        builderGO.AddComponent<BuildingBehavior>().SetBuilder(builder);
        builder.representGameObject = builderGO;

        if (laborCenter != null)
        {
            laborCenter.TeamLockState.Add(teamNumber);

        }

        builder.representGameObject.AddComponent<BuildTimer>();
        builderGO.SetActive(true);
        Debug.Log("Create action complete.");
        LoadManager.Instance.SavePlayerDataToJson();

        return true;

    }

    public void LoadBuilding(Builder builder)
    {
        Building buildData = LoadManager.Instance.allBuildingData[builder.Type];

        GameObject builderGO = Instantiate( Resources.Load<GameObject>("Prefabs/BuildingPrefab"), builder.Position, Quaternion.identity);
        builderGO.SetActive(false);
        builder.InitializeData();
        builderGO.transform.SetParent(this.gameObject.transform.GetChild(0).transform);
        builder.representGameObject = builderGO;

        builderGO.GetComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>(buildData.spritePath[builder.Level]);

        AddBuildingsToList(builder);
        builderGO.name = builder.ID.ToString();
        builderGO.AddComponent<BuildingBehavior>().SetBuilder(builder);

        /// Reconnect reference. -----------------
        for (int i = builder.CharacterInBuilding.Count - 1; i >= 0; i--)
        {
            for (int j = builder.CharacterInBuilding[i].Characters.Count - 1; j >= 0; j--)
            {
                builder.CharacterInBuilding[i].Characters[j] = CharacterManager.Instance.AllCharacters.Single(c => c.ID == builder.CharacterInBuilding[i].Characters[j].ID);
            
            }

        }
        /// --------------------------------------

        if (builder.constructionStatus.isConstructing == true)
        {
            builderGO.AddComponent<BuildTimer>();

        }
        builderGO.SetActive(true);
        Debug.Log("Loading Building From JSON : " + builder.ToString());
        return;

    }

    void AddBuildingsToList(Builder builder)
    {
        allBuildings.Add(builder);

        Builder[] sameTypeBuilding = allBuildings.Where(b1 => b1.Type == builder.Type).ToArray();
        if (sameTypeBuilding != null)
        {
           // Debug.Log(sameTypeBuilding.Length);
            foreach (Builder sameBuilder in sameTypeBuilding)
            {
                sameBuilder.CurrentActiveAmount = sameTypeBuilding.Length;

            }

        }
        else
        {
            Debug.LogError("Something happened on AddBuildingsToList !");

        }
        /// Debug
        //Debug.Log(sameTypeBuilding.Length);
        //foreach (Building builder in sameTypeBuilding)
        //{
        //    Debug.Log(builder.ToString());
        //}
    }
    public void ForceCreateBuilding(Building.BuildingType type, int cellIndex)
    {
        MapManager mapManager = MapManager.Instance;
        var position = mapManager.CalculateBuildPosition(cellIndex);

        GameObject builderGO = Instantiate(Resources.Load<GameObject>("Prefabs/BuildingPrefab"), position, Quaternion.identity);
        builderGO.SetActive(false);
        builderGO.transform.SetParent(this.gameObject.transform.Find("AllBuildings"));

        Builder builder = new Builder(type, position, builderGO);
        Building buildData = LoadManager.Instance.allBuildingData[builder.Type];

        builder.InitializeData();
        builder.constructionStatus = new Builder.Construction { teamNumber = 0, currentPoint = buildData.upgradePoint[builder.Level], constructPointRequired = buildData.upgradePoint[builder.Level], isConstructing = true };
        AddBuildingsToList(builder);

        builderGO.GetComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>(buildData.spritePath[builder.Level]);
        builderGO.name = builder.ID.ToString();
        builderGO.AddComponent<BuildingBehavior>().SetBuilder(builder);
        builder.representGameObject = builderGO;

        BuildTimer buildTimer = builder.representGameObject.AddComponent<BuildTimer>();
        buildTimer.ForceFinish();
        builderGO.SetActive(true);


        mapManager.constructableGrid.RemoveAt(cellIndex);

    }
    public bool UpgradeBuilding(Builder builder, int teamNumber)
    {
        if (builder.constructionStatus.isConstructing == true)
        {
            Debug.LogWarning("You can't perform 2 constructing task on same Building!");
            return false;

        }
        if (builder.maxLevel == builder.Level)
        {
            Debug.LogWarning($"This building({builder.Type}) is on MAX LEVEL {builder.Level}/{builder.maxLevel}");
            return false;

        }
        Builder laborCenter = allBuildings.SingleOrDefault(b => b.Type == Building.BuildingType.LaborCenter);
        if (laborCenter == null)
        {
            if (gameObject.transform.Find("TimerCanvas").childCount == 1)
            {
                Debug.LogWarning("Please Create LaborCenter to perform more Building task !");
                return false;

            }
           
        }
        else
        {
            allBuildings.SingleOrDefault(b => b.Type == Building.BuildingType.LaborCenter).TeamLockState.Add(teamNumber);
       
        }

        if (ConsumeBuildingCost(builder))
        {
            
            builder.constructionStatus.teamNumber = teamNumber;
            builder.representGameObject.AddComponent<BuildTimer>();
            Debug.Log("Upgrade action complete.");
            LoadManager.Instance.SavePlayerDataToJson();
        
        }
        else
        {
            Debug.LogWarning("Upgrade failed : Not enough Resources for upgrading");
            return false;

        }

        return true;

    }

    bool ConsumeBuildingCost(Builder builder)
    {
        Building buildingData = LoadManager.Instance.allBuildingData[builder.Type];
        DictionaryStringToInt buidingCost = buildingData.buildingCost[builder.Level];

        if (ItemManager.Instance.TryConsumeResources(buildingData.buildingCost[builder.Level]))
        {
            return true;

        }
        else
        {
            return false;

        }

    }

    public void RemoveBuilding(Builder destroyBuilding)
    {

        destroyBuilding.CurrentActiveAmount--;
        if (destroyBuilding.constructionStatus.isConstructing)
        {
            destroyBuilding.representGameObject.GetComponent<BuildTimer>().CancelConstructing();
        
        }
        Debug.Log($"Try removing {destroyBuilding.ToString()} . . ");

        foreach (CharacterWrapper cw in destroyBuilding.CharacterInBuilding)
        {
            foreach (Character character in cw.Characters)
            {
                CharacterManager.Instance.CancleAssignWork(character, destroyBuilding);

            }

        }

        MapManager.Instance.ReclaimConstructableGrid(destroyBuilding);
        RemoveBuildingFromList(destroyBuilding);
        Destroy(destroyBuilding.representGameObject, 0.1f);

        LoadManager.Instance.SavePlayerDataToJson();

        Resources.FindObjectsOfTypeAll<BuildingShopPanel>()[0].RefreshPanel();

        return;

    }
    public void RemoveBuildingFromList(Builder b)
    {
        allBuildings.Remove(b);
        Builder[] sameTypeBuildings = allBuildings.Where(b1 => b1.Type == b.Type).ToArray();
        if (sameTypeBuildings != null)
        {
            foreach (Builder builder in sameTypeBuildings)
            {
                builder.CurrentActiveAmount = sameTypeBuildings.Length;

            }

        }
        else
        {
            Debug.LogError("Something happened on RemoveBuildingFromList !");

        }

    }

}
