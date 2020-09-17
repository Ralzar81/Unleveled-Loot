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


namespace UnleveledLootMod
{
    public class UnleveledLoot : MonoBehaviour
    {
        static Mod mod;
        static int luckMod;
        static int matRoll;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<UnleveledLoot>();
        }

        void Awake()
        {
            //Unleveled gold.
            FormulaHelper.RegisterOverride(mod, "ModifyFoundLootItems", (Func<DaggerfallUnityItem[], int>)UnleveledGoldLootPiles);
            EnemyDeath.OnEnemyDeath += UnlevelDroppedLoot_OnEnemyDeath;

            //Override of material mechanics.
            FormulaHelper.RegisterOverride<Func<int, WeaponMaterialTypes>>(mod, "RandomMaterial", (player) =>
            {
                luckMod = GameManager.Instance.PlayerEntity.Stats.LiveLuck / 10;
                matRoll = UnityEngine.Random.Range(0, 91) + luckMod;
                WeaponMaterialTypes material = WpnMatSelector();
                Debug.Log("[Unleveled Loot] matRoll = " + matRoll.ToString());
                return material;
            });
            FormulaHelper.RegisterOverride<Func<int, ArmorMaterialTypes>>(mod, "RandomArmorMaterial", (player) =>
            {
                luckMod = GameManager.Instance.PlayerEntity.Stats.LiveLuck / 10;
                matRoll = UnityEngine.Random.Range(0, 91) + luckMod;
                ArmorMaterialTypes material = ArmMatSelector();
                Debug.Log("[Unleveled Loot] matRoll = " + matRoll.ToString());
                return material;
            });


            mod.IsReady = true;
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
                        Debug.Log(enemyEntity.Name);
                        luckMod = GameManager.Instance.PlayerEntity.Stats.LiveLuck / 10;

                        if (enemyEntity.MobileEnemy.Affinity == MobileAffinity.Daedra)
                        {
                            AddDaedric(entityBehaviour.CorpseLootContainer.Items, enemyEntity.MobileEnemy.ID);
                        }

                        //Code for possibly unleveled gold.
                        List<DaggerfallUnityItem> goldPile = entityBehaviour.CorpseLootContainer.Items.SearchItems(ItemGroups.Currency, (int)Currency.Gold_pieces);
                        foreach (DaggerfallUnityItem gold in goldPile)
                        {
                            gold.stackCount /= GameManager.Instance.PlayerEntity.Level;
                            if (gold.stackCount < 1)
                                gold.stackCount = 1;
                        }
                    }
                }
            }
        }




        private static ArmorMaterialTypes ArmMatSelector()
        {
            ArmorMaterialTypes armorMaterial = ArmorMaterialTypes.Leather;
            int shopMod = 0;
            if (GameManager.Instance.PlayerEnterExit.IsPlayerInsideOpenShop)
                shopMod += GameManager.Instance.PlayerEnterExit.Interior.BuildingData.Quality;

            if (matRoll + shopMod > 60)
            {
                if (matRoll + shopMod > 90)
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
            int region = GameManager.Instance.PlayerGPS.CurrentRegionIndex;
            int dungeon = GameManager.Instance.PlayerEnterExit.Dungeon.Summary.ID;
            int roll = UnityEngine.Random.Range(0, 90) + luckMod;

            //increased chance of orcish when in orsinium or an orc stronghold
            if ((region == (int)DaggerfallRegions.OrsiniumArea || dungeon == (int)DFRegion.DungeonTypes.OrcStronghold) && matRoll > 70)
            {
                if (roll > 90)
                    return WeaponMaterialTypes.Orcish;
            }

            if (matRoll > 98)
            {
                if (roll > 95)
                    weaponMaterial = WeaponMaterialTypes.Daedric;
                else if (roll > 80)
                    weaponMaterial = WeaponMaterialTypes.Orcish;
                else
                    weaponMaterial = WeaponMaterialTypes.Ebony;
            }
            else if (matRoll > 95)
            {
                weaponMaterial = WeaponMaterialTypes.Adamantium;
            }
            else if (matRoll > 90)
            {
                weaponMaterial = WeaponMaterialTypes.Mithril;
            }
            else if (matRoll > 85)
            {
                weaponMaterial = WeaponMaterialTypes.Dwarven;
            }
            else if (matRoll > 75)
            {
                weaponMaterial = WeaponMaterialTypes.Elven;
            }
            else if (matRoll > 70)
            {
                weaponMaterial = WeaponMaterialTypes.Silver;
            }
            else if (matRoll > 30)
            {
                weaponMaterial = WeaponMaterialTypes.Steel;
            }

            return weaponMaterial;
        }

        private static WeaponMaterialTypes shopMat()
        {
            WeaponMaterialTypes weaponMaterial = WeaponMaterialTypes.Steel;
            int region = GameManager.Instance.PlayerGPS.CurrentRegionIndex;
            int shopQuality = GameManager.Instance.PlayerEnterExit.Interior.BuildingData.Quality;
            matRoll = (matRoll - 40) + (shopQuality * 2);

            //increased chance of orcish when in orsinium
            if ((region == (int)DaggerfallRegions.OrsiniumArea) && (matRoll) > 70)
            {
                int roll = UnityEngine.Random.Range(0, 90) + luckMod;
                if (roll > 90)
                    return WeaponMaterialTypes.Orcish;
            }

            if (matRoll > 99)
            {
                weaponMaterial = WeaponMaterialTypes.Daedric;
            }
            else if (matRoll > 97)
            {
                weaponMaterial = WeaponMaterialTypes.Orcish;
            }
            else if (matRoll > 95)
            {
                weaponMaterial = WeaponMaterialTypes.Ebony;
            }
            else if (matRoll > 92)
            {
                weaponMaterial = WeaponMaterialTypes.Adamantium;
            }
            else if (matRoll > 88)
            {
                weaponMaterial = WeaponMaterialTypes.Mithril;
            }
            else if (matRoll > 80)
            {
                weaponMaterial = WeaponMaterialTypes.Dwarven;
            }
            else if (matRoll > 70)
            {
                weaponMaterial = WeaponMaterialTypes.Elven;
            }
            else if (matRoll > 64)
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
                || (enemyEntityID == (int)MobileTypes.DaedraSeducer && roll > 80)
                || ((enemyEntityID == (int)MobileTypes.FireDaedra || enemyEntityID == (int)MobileTypes.FrostDaedra) && roll > 90)
                || (enemyEntityID == (int)MobileTypes.Daedroth && roll > 95))
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

    }
}
