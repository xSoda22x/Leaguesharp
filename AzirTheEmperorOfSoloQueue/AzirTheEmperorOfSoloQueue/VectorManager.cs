using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SharpDX;

namespace AzirTheEmperorOfSoloQueue
{
    class VectorManager
    {
        internal static List<GameObject> AzirObjects = new List<GameObject>();
        internal static void GameObject_OnCreate(GameObject obj, EventArgs args)
        {
//            Game.PrintChat("Object created! "+obj.Name);
            if (obj.Name.Contains("AzirSoldier"))
            {
                AzirObjects.Add(obj);
            }
        }

        internal static GameObject GetSoldierNearPosition(Vector3 pos)
        {
            var soilder = AzirObjects.OrderBy(x => pos.Distance(x.Position));
            if (soilder.FirstOrDefault() != null)
                return soilder.FirstOrDefault();
            return AzirObjects.FirstOrDefault(soldier => pos.Distance(soldier.Position) <= 500f);
        }

        internal static void GameObject_OnDelete(GameObject sender, EventArgs args)
        {
            if (AzirObjects.All(obj => obj.NetworkId != sender.NetworkId)) return;
            foreach (var soldier in AzirObjects.Where(soldier => soldier.NetworkId == sender.NetworkId))
            {
                AzirObjects.Remove(soldier);
                break;
            }
        }
        internal static void RemoveCorruptedSoldiers()
        {
            if (AzirObjects != null && AzirObjects.Count > 0)
                AzirObjects.RemoveAll(soldier => !soldier.IsValid || soldier.IsDead || soldier.Position.Distance(ObjectManager.Player.Position) > 1500);
        }

        internal static Vector3 MaxSoldierPosition(Vector3 position)
        {
            return ObjectManager.Player.Position.Extend(position, 450);
        }

        internal static bool IsWithinSoldierRange(Obj_AI_Base unit)
        {
            return AzirObjects.Any(soldier => unit.Distance(soldier.Position) <= 345f);
        }

        internal static bool IsInFront(Vector3 unit, Vector3 direction)
        {
            var toTarget = (unit - direction).Normalized();
            /* True = Target is in front
             * False = Target is behind
             * */
            return Vector3.Dot(toTarget, direction) > 0;
        }
        internal static bool CanDive(GameObject AzirSoldier)
        {
            if (Emperor.Config.Item("eDive").GetValue<KeyBind>().Active)
            {
                return !(ObjectManager.Player.Position + Vector3.Normalize(AzirSoldier.Position - ObjectManager.Player.Position) * 475).UnderTurret(true);
            }
            return true;
        }
    }
}