using System.Collections.Generic;
using UnityEngine;

namespace TenkaiMenu;

public static class TaskCheats
{
    private static readonly HashSet<NormalPlayerTask> injectedVisualTasks = new();
    private static readonly Dictionary<NormalPlayerTask, PlayerControl> originalVisualTaskOwners = new();

    public static void UpdateVisualTaskAccess()
    {
        if (!CheatToggles.unlockVisualTasks || !Utils.isPlayer || ShipStatus.Instance == null)
        {
            Cleanup();
            return;
        }

        var player = PlayerControl.LocalPlayer;
        if (player == null || player.Data == null || player.Data.IsDead || MeetingHud.Instance != null) return;

        var position = player.GetTruePosition();
        var availableVisualTasks = new HashSet<NormalPlayerTask>();
        NormalPlayerTask[][] taskGroups =
        {
            ShipStatus.Instance.CommonTasks,
            ShipStatus.Instance.LongTasks,
            ShipStatus.Instance.ShortTasks
        };

        foreach (var taskGroup in taskGroups)
        {
            if (taskGroup == null) continue;
            foreach (var task in taskGroup)
            {
                if (task == null || player.myTasks.Contains(task)) continue;
                if (task.TaskType is not (TaskTypes.SubmitScan or TaskTypes.ClearAsteroids or TaskTypes.PrimeShields)) continue;

                try
                {
                    var consoles = task.FindConsoles();
                    if (consoles == null) continue;
                    foreach (var console in consoles)
                    {
                        if (console != null && Vector2.Distance(position, console.transform.position) <= console.UsableDistance)
                        {
                            availableVisualTasks.Add(task);
                            break;
                        }
                    }
                }
                catch { }
            }
        }

        foreach (var task in availableVisualTasks)
        {
            if (injectedVisualTasks.Contains(task)) continue;

            originalVisualTaskOwners[task] = task.Owner;
            task.Owner = player;
            uint nextId = 0;
            foreach (var ownedTask in player.myTasks)
            {
                if (ownedTask.Id >= nextId) nextId = ownedTask.Id + 1;
            }
            task.Id = nextId;
            player.myTasks.Add(task);
            injectedVisualTasks.Add(task);
        }

        var removeVisualTasks = new List<NormalPlayerTask>();
        foreach (var task in injectedVisualTasks)
        {
            if (!availableVisualTasks.Contains(task)) removeVisualTasks.Add(task);
        }

        foreach (var task in removeVisualTasks)
        {
            player.myTasks.Remove(task);
            if (originalVisualTaskOwners.TryGetValue(task, out var owner))
            {
                task.Owner = owner;
                originalVisualTaskOwners.Remove(task);
            }
            injectedVisualTasks.Remove(task);
        }
    }

    public static bool IsAllowedVisualTaskConsole(Console console)
    {
        if (console == null || ShipStatus.Instance == null) return false;

        NormalPlayerTask[][] taskGroups =
        {
            ShipStatus.Instance.CommonTasks,
            ShipStatus.Instance.LongTasks,
            ShipStatus.Instance.ShortTasks
        };

        foreach (var taskGroup in taskGroups)
        {
            if (taskGroup == null) continue;
            foreach (var task in taskGroup)
            {
                if (task == null || task.TaskType is not (TaskTypes.SubmitScan or TaskTypes.ClearAsteroids or TaskTypes.PrimeShields)) continue;

                try
                {
                    var consoles = task.FindConsoles();
                    if (consoles == null) continue;
                    foreach (var taskConsole in consoles)
                    {
                        if (taskConsole == console) return true;
                    }
                }
                catch { }
            }
        }

        return false;
    }

    private static void Cleanup()
    {
        var player = PlayerControl.LocalPlayer;
        if (player != null)
        {
            foreach (var task in injectedVisualTasks) player.myTasks.Remove(task);
        }

        foreach (var entry in originalVisualTaskOwners) entry.Key.Owner = entry.Value;
        injectedVisualTasks.Clear();
        originalVisualTaskOwners.Clear();
    }
}
