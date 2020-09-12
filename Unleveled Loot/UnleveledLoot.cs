using DaggerfallConnect;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.MagicAndEffects;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using DaggerfallWorkshop.Game.Utility.ModSupport.ModSettings;
using UnityEngine;
using System;
using DaggerfallWorkshop;
using DaggerfallConnect.Arena2;
using DaggerfallWorkshop.Utility;
using DaggerfallWorkshop.Game.UserInterfaceWindows;
using DaggerfallWorkshop.Game.Serialization;
using System.Collections.Generic;
using DaggerfallWorkshop.Game.UserInterface;
using DaggerfallConnect.Utility;
using System.Collections;
using DaggerfallConnect.Save;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Player;
using DaggerfallWorkshop.Game.MagicAndEffects.MagicEffects;
using DaggerfallWorkshop.Game.Guilds;

namespace UnleveledLootMod
{
    public class UnleveledLoot : MonoBehaviour
    {
        static Mod mod;

        [Invoke(StateManager.StateTypes.Start, 0)]
        public static void Init(InitParams initParams)
        {
            mod = initParams.Mod;
            var go = new GameObject(mod.Title);
            go.AddComponent<UnleveledLoot>();
        }

        void Awake()
        {
            FormulaHelper.RegisterOverride(mod, "ModifyFoundLootItems", (Func<DaggerfallUnityItem[], int>)UnleveledFoundLootItems);
            EnemyDeath.OnEnemyDeath += UnlevelDroppedLoot_OnEnemyDeath;
            mod.IsReady = true;
        }

        static int luckMod;

        public static void UnlevelDroppedLoot_OnEnemyDeath(object sender, EventArgs e)
        {            
            EnemyDeath enemyDeath = sender as EnemyDeath;
            if (enemyDeath != null)
            {
                DaggerfallEntityBehaviour entityBehaviour = enemyDeath.GetComponent<DaggerfallEntityBehaviour>();
                if (entityBehaviour != null)
                {
                    Debug.Log(entityBehaviour.Entity.Name);
                    EnemyEntity enemyEntity = entityBehaviour.Entity as EnemyEntity;
                    if (enemyEntity != null)
                    {
                        Debug.Log(enemyEntity.Name);
                        luckMod = GameManager.Instance.PlayerEntity.Stats.LiveLuck / 10 + EnemyMod(enemyEntity);

                        if (enemyEntity.MobileEnemy.Affinity == MobileAffinity.Human || HumanoidCheck(enemyEntity.MobileEnemy.ID))
                        {
                            LootPileChecker(entityBehaviour.CorpseLootContainer.Items, enemyEntity.MobileEnemy.ID);
                        }
                        if (enemyEntity.MobileEnemy.Affinity == MobileAffinity.Daedra)
                        {
                            AddDaedric(entityBehaviour.CorpseLootContainer.Items, enemyEntity.MobileEnemy.ID);
                        }
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

        private static int EnemyMod(EnemyEntity enemyEntity)
        {
            int mod = 0;

            switch (enemyEntity.MobileEnemy.ID)
            {
                case (int)MobileTypes.AncientLich:
                    mod += 20;
                    break;
                case (int)MobileTypes.Lich:
                case (int)MobileTypes.VampireAncient:
                    mod += 15;
                    break;
                case (int)MobileTypes.Knight:
                case (int)MobileTypes.OrcWarlord:
                    mod += 10;
                    break;
                case (int)MobileTypes.OrcSergeant:
                case (int)MobileTypes.Warrior:
                    mod += 5;
                    break;
            }

            return mod;
        }

        private static void LootPileChecker(ItemCollection lootStack, int enemyEntityID)
        {
            ItemCollection newItems = new ItemCollection();

            for (int i = 0; i < lootStack.Count; i++)
            {
                DaggerfallUnityItem item = lootStack.GetItem(i);
                if (item.ItemGroup == ItemGroups.Weapons)
                {
                    DaggerfallUnityItem newWpn = WpnMatChanger(item, enemyEntityID);
                    newItems.AddItem(newWpn);
                    lootStack.RemoveItem(item);
                }
                else if (item.ItemGroup == ItemGroups.Armor)
                {
                    DaggerfallUnityItem newArm = ArmMatChanger(item, enemyEntityID);
                    newItems.AddItem(newArm);
                    lootStack.RemoveItem(item);
                }
            }



            for (int i = 0; i < newItems.Count; i++)
            {
                DaggerfallUnityItem item = newItems.GetItem(i);
                lootStack.AddItem(item);
            }
        }

        private static DaggerfallUnityItem WpnMatChanger(DaggerfallUnityItem oldWpn, int enemyEntityID)
        {
            float condMultiplier = oldWpn.currentCondition / oldWpn.maxCondition;
            WeaponMaterialTypes material = WpnMatSelector(enemyEntityID);

            DaggerfallUnityItem newWpn = ItemBuilder.CreateItem(ItemGroups.Weapons, oldWpn.TemplateIndex);

            ItemBuilder.ApplyWeaponMaterial(newWpn, material);
            newWpn.currentCondition = (int)condMultiplier * newWpn.maxCondition;
            return newWpn;
        }

        private static DaggerfallUnityItem ArmMatChanger(DaggerfallUnityItem oldArm, int enemyEntityID)
        {
            float condMultiplier = oldArm.currentCondition / oldArm.maxCondition;
            ArmorMaterialTypes material = ArmMatSelector(enemyEntityID);

            DaggerfallUnityItem newArm = ItemBuilder.CreateItem(ItemGroups.Armor, oldArm.TemplateIndex);

            ItemBuilder.ApplyArmorMaterial(newArm, material);
            newArm.currentCondition = (int)condMultiplier * newArm.maxCondition;
            return newArm;
        }

        private static WeaponMaterialTypes WpnMatSelector(int enemyEntityID)
        {
            WeaponMaterialTypes material = WeaponMaterialTypes.Iron;
            int roll = UnityEngine.Random.Range(0,101) + (luckMod - 5);
            if (enemyEntityID == (int)MobileTypes.Orc && roll > 95)
            {
                material = WeaponMaterialTypes.Orcish;
            }
            else if (enemyEntityID == (int)MobileTypes.OrcSergeant && roll > 90)
            {
                material = WeaponMaterialTypes.Orcish;
            }
            else if (enemyEntityID == (int)MobileTypes.OrcShaman && roll > 80)
            {
                material = WeaponMaterialTypes.Orcish;
            }
            else if (enemyEntityID == (int)MobileTypes.Orc && roll > 60)
            {
                material = WeaponMaterialTypes.Orcish;
            }
            else if (roll > 100)
            {
                material = WeaponMaterialTypes.Daedric;
            }
            else if (roll > 99)
            {
                material = WeaponMaterialTypes.Orcish;
            }
            else if (roll > 96)
            {
                material = WeaponMaterialTypes.Ebony;
            }
            else if (roll > 92)
            {
                material = WeaponMaterialTypes.Adamantium;
            }
            else if (roll > 87)
            {
                material = WeaponMaterialTypes.Mithril;
            }
            else if (roll > 81)
            {
                material = WeaponMaterialTypes.Dwarven;
            }
            else if (roll > 74)
            {
                material = WeaponMaterialTypes.Elven;
            }
            else if (roll > 66)
            {
                material = WeaponMaterialTypes.Silver;
            }
            else if (roll > 31)
            {
                material = WeaponMaterialTypes.Steel;
            }
            return material;
        }

        private static ArmorMaterialTypes ArmMatSelector(int enemyEntityID)
        {
            ArmorMaterialTypes material = ArmorMaterialTypes.Iron;
            int roll = UnityEngine.Random.Range(0, 101) + (luckMod - 5);
            if (enemyEntityID == (int)MobileTypes.Orc && roll > 95)
            {
                material = ArmorMaterialTypes.Orcish;
            }
            else if (enemyEntityID == (int)MobileTypes.OrcSergeant && roll > 90)
            {
                material = ArmorMaterialTypes.Orcish;
            }
            else if (enemyEntityID == (int)MobileTypes.OrcShaman && roll > 80)
            {
                material = ArmorMaterialTypes.Orcish;
            }
            else if (enemyEntityID == (int)MobileTypes.Orc && roll > 60)
            {
                material = ArmorMaterialTypes.Orcish;
            }
            else if (roll > 100)
            {
                material = ArmorMaterialTypes.Daedric;
            }
            else if (roll > 99)
            {
                material = ArmorMaterialTypes.Orcish;
            }
            else if (roll > 96)
            {
                material = ArmorMaterialTypes.Ebony;
            }
            else if (roll > 92)
            {
                material = ArmorMaterialTypes.Adamantium;
            }
            else if (roll > 87)
            {
                material = ArmorMaterialTypes.Mithril;
            }
            else if (roll > 81)
            {
                material = ArmorMaterialTypes.Dwarven;
            }
            else if (roll > 74)
            {
                material = ArmorMaterialTypes.Elven;
            }
            else if (roll > 66)
            {
                material = ArmorMaterialTypes.Silver;
            }
            else if (roll > 31)
            {
                material = ArmorMaterialTypes.Steel;
            }
            return material;
        }

        private static void AddDaedric(ItemCollection loot, int enemyEntityID)
        {
            int roll = UnityEngine.Random.Range(0, 101) + (luckMod - 5);
            bool giveItem = false;
            if (enemyEntityID == (int)MobileTypes.DaedraLord && roll > 60)
            {
                giveItem = true;
            }
            else if (enemyEntityID == (int)MobileTypes.DaedraSeducer && roll > 70)
            {
                giveItem = true;
            }
            else if (enemyEntityID == (int)MobileTypes.Daedroth && roll > 80)
            {
                giveItem = true;
            }
            else if ((enemyEntityID == (int)MobileTypes.FireDaedra || enemyEntityID == (int)MobileTypes.FrostDaedra) && roll > 90)
            {
                giveItem = true;
            }

            if (giveItem)
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
















        public static int UnleveledFoundLootItems(DaggerfallUnityItem[] lootItems)
        {
            Debug.Log("[Unleveled Loot] Method starting");
            int changes = 0;
            for (int i = 0; i < lootItems.Length; i++)
            {
                DaggerfallUnityItem item = lootItems[i];
                if (item.ItemGroup == ItemGroups.Books && !item.IsArtifact)
                {
                    // Apply a random condition between 20% and 70%.
                    float conditionMod = UnityEngine.Random.Range(0.2f, 0.75f);
                    item.currentCondition = (int)(item.maxCondition * conditionMod);
                    Debug.Log("[Unleveled Loot] Book");
                    changes++;
                }
                else if ((item.ItemGroup == ItemGroups.Armor || item.ItemGroup == ItemGroups.Weapons) && !item.IsArtifact && item.nativeMaterialValue != (int)ArmorMaterialTypes.Leather && item.nativeMaterialValue != (int)ArmorMaterialTypes.Chain && item.nativeMaterialValue != (int)ArmorMaterialTypes.Chain2)
                {
                    float conditionMod = UnityEngine.Random.Range(0.2f, 0.75f);
                    item.currentCondition = (int)(item.maxCondition * conditionMod);
                    Debug.Log("[Unleveled Loot] Armor/Weapons");
                    materialChanger(item);
                    changes++;
                }
            }
            return changes;
        }

        private static void materialChanger(DaggerfallUnityItem item)
        {
            int luck = GameManager.Instance.PlayerEntity.Stats.LiveLuck;
            int matRoll = 0;
            matRoll = UnityEngine.Random.Range(0, 100) + luck;
            if (item.ItemGroup == ItemGroups.Armor)
                item.nativeMaterialValue = (int)ArmorMaterialTypes.Daedric;
            else
                item.nativeMaterialValue = (int)WeaponMaterialTypes.Daedric;
        }




        private static bool HumanoidCheck(int enemyID)
        {
            switch (enemyID)
            {
                case (int)MobileTypes.Orc:
                case (int)MobileTypes.Centaur:
                case (int)MobileTypes.OrcSergeant:
                case (int)MobileTypes.Giant:
                case (int)MobileTypes.OrcShaman:
                case (int)MobileTypes.OrcWarlord:
                    return true;
            }
            return false;
        }

        private static bool OrcCheck(int enemyID)
        {
            switch (enemyID)
            {
                case (int)MobileTypes.Orc:
                case (int)MobileTypes.OrcSergeant:
                case (int)MobileTypes.OrcShaman:
                case (int)MobileTypes.OrcWarlord:
                    return true;
            }
            return false;
        }


















    }
}
