using DaggerfallConnect;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop;
using UnityEngine;
using System;
using System.Collections.Generic;
using DaggerfallWorkshop.Game.Utility;

namespace UnleveledLootMod
{
    public class UnleveledLoot : MonoBehaviour
    {
        static Mod mod;
        static int luckMod;
        static int matRoll;
        static int region = 0;
        static int dungeon = 0;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<UnleveledLoot>();
        }
        
        void Awake()
        {
            //Unleveled gold and daedra drops
            FormulaHelper.RegisterOverride(mod, "ModifyFoundLootItems", (Func<DaggerfallUnityItem[], int>)UnleveledGoldLootPiles);
            EnemyDeath.OnEnemyDeath += UnlevelDroppedLoot_OnEnemyDeath;

            PlayerEnterExit.OnPreTransition += SetDungeon_OnPreTransition;
            PlayerEnterExit.OnTransitionExterior += ClearData_OnTransitionExterior;

            //Override of material mechanics.
            FormulaHelper.RegisterOverride<Func<int, WeaponMaterialTypes>>(mod, "RandomMaterial", (player) =>
            {
                luckMod = GameManager.Instance.PlayerEntity.Stats.LiveLuck / 10;
                matRoll = UnityEngine.Random.Range(0, 92) + luckMod;
                WeaponMaterialTypes material = WpnMatSelector();
                return material;
            });
            FormulaHelper.RegisterOverride<Func<int, ArmorMaterialTypes>>(mod, "RandomArmorMaterial", (player) =>
            {
                luckMod = GameManager.Instance.PlayerEntity.Stats.LiveLuck / 10;
                matRoll = UnityEngine.Random.Range(0, 92) + luckMod;
                ArmorMaterialTypes material = ArmMatSelector();
                return material;
            });


            mod.IsReady = true;
        }

        private static void SetDungeon_OnPreTransition(PlayerEnterExit.TransitionEventArgs args)
        {
            region = GameManager.Instance.PlayerGPS.CurrentRegionIndex;
            dungeon = (int)GameManager.Instance.PlayerGPS.CurrentLocation.MapTableData.DungeonType;
        }

        private static void ClearData_OnTransitionExterior(PlayerEnterExit.TransitionEventArgs args)
        {
            dungeon = -1;
        }

        //Code for possibly unleveled gold.
        public static int UnleveledGoldLootPiles(DaggerfallUnityItem[] lootItems)
        {
            Debug.Log("[Unleveled Loot] Method starting");
            luckMod = GameManager.Instance.PlayerEntity.Stats.LiveLuck / 10;
            int changes = 0;
            for (int i = 0; i < lootItems.Length; i++)
            {
                DaggerfallUnityItem item = lootItems[i];
                if (item.ItemGroup == ItemGroups.Books && !item.IsArtifact)
                {
                    float conditionMod = UnityEngine.Random.Range(0.2f, 0.75f);
                    item.currentCondition = (int)(item.maxCondition * conditionMod);
                    Debug.Log("[Unleveled Loot] Book");
                    changes++;
                }
                else if (item.ItemGroup == ItemGroups.Currency)
                {
                    item.stackCount /= GameManager.Instance.PlayerEntity.Level;
                    if (item.stackCount < 1)
                        item.stackCount = 1;
                }
            }
            return changes;
        }

        public static void UnlevelDroppedLoot_OnEnemyDeath(object sender, EventArgs e)
        {
            EnemyDeath enemyDeath = sender as EnemyDeath;
            if (enemyDeath != null)
            {
                DaggerfallEntityBehaviour entityBehaviour = enemyDeath.GetComponent<DaggerfallEntityBehaviour>();
                if (entityBehaviour != null)
                {
                    EnemyEntity enemyEntity = entityBehaviour.Entity as EnemyEntity;
                    if (enemyEntity != null)
                    {
                        luckMod = GameManager.Instance.PlayerEntity.Stats.LiveLuck / 10;

                        if (enemyEntity.MobileEnemy.Affinity == MobileAffinity.Daedra)
                        {
                            AddDaedric(entityBehaviour.CorpseLootContainer.Items, enemyEntity.MobileEnemy.ID);
                        }
                        else if (enemyEntity.MobileEnemy.Team == MobileTeams.Orcs)
                        {
                            AddOrcish(entityBehaviour.CorpseLootContainer.Items, enemyEntity.MobileEnemy.ID);
                        }

                        //Code for possibly unleveled gold.
                        List<DaggerfallUnityItem> goldPile = entityBehaviour.CorpseLootContainer.Items.SearchItems(ItemGroups.Currency, (int)Currency.Gold_pieces);
                        foreach (DaggerfallUnityItem gold in goldPile)
                        {
                            gold.stackCount /= GameManager.Instance.PlayerEntity.Level;
                            if (gold.stackCount < 1)
                                gold.stackCount = 1;
                            gold.stackCount *= UnityEngine.Random.Range(1, luckMod + 1);
                        }
                    }
                }
            }
        }




        private static ArmorMaterialTypes ArmMatSelector()
        {
            ArmorMaterialTypes armorMaterial = ArmorMaterialTypes.Leather;
            int rollMod = 0;
            int armRoll = UnityEngine.Random.Range(1, 91) + luckMod;

            if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideOpenShop)
                rollMod += GameManager.Instance.PlayerEnterExit.Interior.BuildingData.Quality * 6;
            else if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeon)
                rollMod += dungeonQuality();
            if (armRoll + rollMod > 50)
            {
                if (armRoll + rollMod > 80)
                {
                    WeaponMaterialTypes material = WpnMatSelector();
                    armorMaterial = (ArmorMaterialTypes)(0x0200 + material);
                }
                else
                    armorMaterial = ArmorMaterialTypes.Chain;
            }
            return armorMaterial;
        }


        private static WeaponMaterialTypes WpnMatSelector()
        {
            WeaponMaterialTypes weaponMaterial = WeaponMaterialTypes.Iron;
            if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideOpenShop)
                weaponMaterial = shopMat();
            else
                weaponMaterial = randomMat();

            return weaponMaterial;
        }



        private static WeaponMaterialTypes randomMat()
        {
            WeaponMaterialTypes weaponMaterial = WeaponMaterialTypes.Iron;
            if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideDungeon)
                matRoll = (matRoll - 15) + dungeonQuality();
            else
                matRoll -= UnityEngine.Random.Range(10, 20);
            //increased chance of orcish when in orsinium or an orc stronghold
            if (Dice100.SuccessRoll(luckMod * 2))
            {
                if ((region == (int)DaggerfallRegions.OrsiniumArea || dungeon == (int)DFRegion.DungeonTypes.OrcStronghold) && Dice100.SuccessRoll(luckMod))
                {
                    return WeaponMaterialTypes.Orcish;
                }
                else
                    matRoll += luckMod * 4;               
            }
            if (matRoll > 90)
            {
                if (Dice100.SuccessRoll(luckMod * 3))
                    weaponMaterial = RandomHighTier();
                else
                    weaponMaterial = WeaponMaterialTypes.Dwarven;
            }
            else if (matRoll > 83)
            {
                weaponMaterial = WeaponMaterialTypes.Elven;
            }
            else if (matRoll > 75)
            {
                weaponMaterial = WeaponMaterialTypes.Silver;
            }
            else if (matRoll > 35)
            {
                weaponMaterial = WeaponMaterialTypes.Steel;
            }
            return weaponMaterial;
        }

        private static WeaponMaterialTypes RandomHighTier()
        {
            WeaponMaterialTypes weaponMaterial = WeaponMaterialTypes.Mithril;

            int roll = UnityEngine.Random.Range(0, 201);
            if (roll > 199)
                weaponMaterial = WeaponMaterialTypes.Daedric;
            else if (roll > 192)
                weaponMaterial = WeaponMaterialTypes.Orcish;
            else if (roll > 180)
                weaponMaterial = WeaponMaterialTypes.Ebony;
            else if (roll > 100)
                weaponMaterial = WeaponMaterialTypes.Adamantium;


            return weaponMaterial;
        }

        private static int dungeonQuality()
        {
            switch (dungeon)
            {
                case (int)DFRegion.DungeonTypes.VolcanicCaves:
                case (int)DFRegion.DungeonTypes.Coven:
                    return 21;
                case (int)DFRegion.DungeonTypes.DesecratedTemple:
                case (int)DFRegion.DungeonTypes.DragonsDen:
                    return 18;
                case (int)DFRegion.DungeonTypes.BarbarianStronghold:
                case (int)DFRegion.DungeonTypes.VampireHaunt:
                    return 15;
                case (int)DFRegion.DungeonTypes.Crypt:
                case (int)DFRegion.DungeonTypes.OrcStronghold:
                    return 12;
                case (int)DFRegion.DungeonTypes.Laboratory:
                case (int)DFRegion.DungeonTypes.HarpyNest:
                    return 10;
                case (int)DFRegion.DungeonTypes.GiantStronghold:
                    return 5;
                case (int)DFRegion.DungeonTypes.HumanStronghold:
                case (int)DFRegion.DungeonTypes.RuinedCastle:
                case (int)DFRegion.DungeonTypes.Prison:
                    return Mathf.Min(GameManager.Instance.PlayerEntity.Level, 15);
            }

            return 0;
        }

        private static WeaponMaterialTypes shopMat()
        {
            WeaponMaterialTypes weaponMaterial = WeaponMaterialTypes.Steel;
            int region = GameManager.Instance.PlayerGPS.CurrentRegionIndex;
            int shopQuality = GameManager.Instance.PlayerEnterExit.Interior.BuildingData.Quality;
            matRoll = (matRoll - 35) + (shopQuality * 2);

            //increased chance of orcish when in orsinium
            if ((region == (int)DaggerfallRegions.OrsiniumArea) && (matRoll) > 70)
            {
                int roll = UnityEngine.Random.Range(0, 90) + luckMod;
                if (roll > 90)
                    return WeaponMaterialTypes.Orcish;
            }

            if (matRoll > 99)
            {
                weaponMaterial = WeaponMaterialTypes.Ebony;
            }
            else if (matRoll > 97)
            {
                weaponMaterial = WeaponMaterialTypes.Adamantium;
            }
            else if (matRoll > 94)
            {
                weaponMaterial = WeaponMaterialTypes.Mithril;
            }
            else if (matRoll > 88)
            {
                weaponMaterial = WeaponMaterialTypes.Dwarven;
            }
            else if (matRoll > 80)
            {
                weaponMaterial = WeaponMaterialTypes.Elven;
            }
            else if (matRoll > 70)
            {
                weaponMaterial = WeaponMaterialTypes.Silver;
            }
            else if (matRoll < 10)
            {
                weaponMaterial = WeaponMaterialTypes.Iron;
            }
            return weaponMaterial;
        }


        private static void AddDaedric(ItemCollection loot, int enemyEntityID)
        {
            int roll = UnityEngine.Random.Range(0, 101) + (luckMod - 5);
            bool addDaedricItem = false;
            if ((enemyEntityID == (int)MobileTypes.DaedraLord && roll > 70)
                || (enemyEntityID == (int)MobileTypes.DaedraSeducer && roll > 75)
                || ((enemyEntityID == (int)MobileTypes.FireDaedra || enemyEntityID == (int)MobileTypes.FrostDaedra) && roll > 80)
                || (enemyEntityID == (int)MobileTypes.Daedroth && roll > 80))
            {
                addDaedricItem = true;
            }

            if (addDaedricItem)
            {
                int typeRoll = UnityEngine.Random.Range(0, 100);
                DaggerfallUnityItem item;
                if (typeRoll > 50 || enemyEntityID == (int)MobileTypes.DaedraSeducer)
                {
                    Weapons weapon = (Weapons)UnityEngine.Random.Range((int)Weapons.Dagger, (int)Weapons.Long_Bow + 1);
                    item = ItemBuilder.CreateItem(ItemGroups.Weapons, (int)weapon);
                    ItemBuilder.ApplyWeaponMaterial(item, WeaponMaterialTypes.Daedric);
                    item.currentCondition = (int)(UnityEngine.Random.Range(0.3f, 0.75f) * item.maxCondition);
                    loot.AddItem(item);
                }
                else
                {
                    Armor armor = (Armor)UnityEngine.Random.Range((int)Armor.Cuirass, (int)Armor.Tower_Shield + 1);
                    item = ItemBuilder.CreateItem(ItemGroups.Armor, (int)armor);
                    ItemBuilder.ApplyArmorMaterial(item, ArmorMaterialTypes.Daedric);
                    item.currentCondition = (int)(UnityEngine.Random.Range(0.3f, 0.75f) * item.maxCondition);
                    loot.AddItem(item);
                }
            }
        }

        private static void AddOrcish(ItemCollection loot, int enemyEntityID)
        {
            int roll = UnityEngine.Random.Range(0, 101) + (luckMod - 5);
            bool addOrcishItem = false;
            if ((enemyEntityID == (int)MobileTypes.OrcWarlord && roll > 80)
                || (enemyEntityID == (int)MobileTypes.OrcShaman && roll > 90)
                || (enemyEntityID == (int)MobileTypes.OrcSergeant && roll > 95)
                || (enemyEntityID == (int)MobileTypes.Orc && roll > 98))
            {
                addOrcishItem = true;
            }

            if (addOrcishItem)
            {
                int typeRoll = UnityEngine.Random.Range(0, 100);
                DaggerfallUnityItem item;
                if (typeRoll > 50)
                {
                    Weapons weapon = (Weapons)UnityEngine.Random.Range((int)Weapons.Dagger, (int)Weapons.Long_Bow + 1);
                    item = ItemBuilder.CreateItem(ItemGroups.Weapons, (int)weapon);
                    ItemBuilder.ApplyWeaponMaterial(item, WeaponMaterialTypes.Orcish);
                    item.currentCondition = (int)(UnityEngine.Random.Range(0.3f, 0.75f) * item.maxCondition);
                    loot.AddItem(item);
                }
                else
                {
                    Armor armor = (Armor)UnityEngine.Random.Range((int)Armor.Cuirass, (int)Armor.Tower_Shield + 1);
                    item = ItemBuilder.CreateItem(ItemGroups.Armor, (int)armor);
                    ItemBuilder.ApplyArmorMaterial(item, ArmorMaterialTypes.Orcish);
                    item.currentCondition = (int)(UnityEngine.Random.Range(0.3f, 0.75f) * item.maxCondition);
                    loot.AddItem(item);
                }
            }
        }
    }
}