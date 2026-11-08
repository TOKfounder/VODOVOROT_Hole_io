using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Vodovorot.Stage3;
using PinePie.SimpleJoystick.Examples.DemoScript;

/// <summary>Physical trigger/exit/growth/pool regression, independent of the dispatch implementation.</summary>
public static class CheckAbsorptionContacts
{
    private static readonly List<object> Checks = new List<object>();
    private static void Check(bool value, string name)
    {
        Checks.Add(new { name, passed = value });
        if (!value) throw new Exception(name);
    }
    private static async Task WaitUntil(Func<bool> condition, int milliseconds = 2200)
    {
        for (int elapsed = 0; elapsed < milliseconds && !condition(); elapsed += 50)
            await Task.Delay(50);
    }
    private static void PlacePlayer(BlackHoleController player, Vector3 point)
    {
        player.transform.position = new Vector3(point.x, .164f, point.z);
        Physics.SyncTransforms();
    }
    private static void Prepare(FallingObject item, Vector3 point)
    {
        item.transform.position = point;
        // This fixture teleports an existing collider far from its authored pose.
        // Synchronize before asking the production method to cache spawn bounds.
        Physics.SyncTransforms();
        item.PrepareForSpawn(false);
        Physics.SyncTransforms();
    }
    public static Task<string> Baseline() => Run("baseline");

    private static async Task<string> Run(string label)
    {
        Checks.Clear();
        string failure = null;
        byte[] save = File.ReadAllBytes("Assets/PluginYourGames/Editor/SavesEditorYG2.json");
        try
        {
            Check(Application.isPlaying && RankSimulationContext.Active && YG.YG2.saves.musicValue == 0 && YG.YG2.saves.soundValue == 0, "isolated silent physical fixture");
            GamingManager game = UnityEngine.Object.FindAnyObjectByType<GamingManager>();
            game.HandleTimer(false);
            BlackHoleController player = UnityEngine.Object.FindAnyObjectByType<BlackHoleController>();
            player.GetComponentInChildren<MovementScript>().enabled = false;
            player.InitializeScore(0, true);
            PlacePlayer(player, new Vector3(500, 0, 510));
            await Task.Delay(100);
            FallingObject[] props = UnityEngine.Object.FindObjectsByType<FallingObject>(FindObjectsInactive.Exclude);
            FallingObject small = props.First(item => item.IsAvailableTarget && !item.isColon &&
                item.CanBeEatenBy(player) && Mathf.Max(item.size.x, item.size.z) < .35f && item.size.y > .1f);
            Vector3 smallPoint = new Vector3(500, .18f, 500);
            Prepare(small, smallPoint);
            int amount = small.value;
            await Task.Delay(150);
            Check(!small.isTriggered && small.value == amount, "no contact leaves idle object intact");
            PlacePlayer(player, smallPoint);
            await WaitUntil(() => small.isTriggered || small.value == 0);
            Check(small.isTriggered || small.value == 0, "real trigger contact activates idle fitting object");
            await WaitUntil(() => small.value == 0);
            Check(small.value == 0 && player.score == amount, "real physical fall scores fitting object once");
            await Task.Delay(150);
            Check(player.score == amount && !small.GetComponent<Collider>().enabled, "scored object cannot score again on staying contact");

            PlacePlayer(player, new Vector3(520, 0, 510));
            player.InitializeScore(0, true);
            await Task.Delay(100);
            FallingObject large = props.First(item => item != small && item.IsAvailableTarget && !item.isColon &&
                !item.CanBeEatenBy(player) && player.RequiredSizeFor(item.size) == 2 && Mathf.Max(item.size.x, item.size.z) < 1.3f);
            Vector3 largePoint = new Vector3(520, .18f, 500);
            Prepare(large, largePoint);
            int largeAmount = large.value;
            PlacePlayer(player, largePoint);
            await Task.Delay(200);
            Check(!large.isTriggered && large.GetComponent<Rigidbody>().isKinematic && large.value == largeAmount, "oversized contact remains blocked at initial size");
            player.InitializeScore(26, true);
            await WaitUntil(() => large.isTriggered || large.value == 0);
            Check(large.isTriggered || large.value == 0, "growth while staying in trigger activates same candidate without re-entry");

            PlacePlayer(player, new Vector3(540, 0, 510));
            player.InitializeScore(0, true);
            large.ResetToStart();
            Prepare(large, new Vector3(540, .18f, 500));
            PlacePlayer(player, new Vector3(540, 0, 500));
            await Task.Delay(200);
            Check(!large.isTriggered, "reset candidate blocks again at small size");
            PlacePlayer(player, new Vector3(540, 0, 515));
            await Task.Delay(150);
            player.InitializeScore(26, true);
            await Task.Delay(200);
            Check(!large.isTriggered && large.CurrentHole == null, "exit removes candidate: remote growth cannot activate it");
            PlacePlayer(player, new Vector3(540, 0, 500));
            await WaitUntil(() => large.isTriggered || large.value == 0);
            Check(large.isTriggered || large.value == 0, "real re-entry after exit activates fitting reset object");

            PlacePlayer(player, new Vector3(500, 0, 515));
            player.InitializeScore(0, true);
            Prepare(small, smallPoint);
            Check(small.value == amount && small.IsAvailableTarget, "pool preparation restores value, renderer, collider and target eligibility");
            PlacePlayer(player, smallPoint);
            await WaitUntil(() => small.value == 0);
            Check(small.value == 0 && player.score == amount, "pooled object re-enters physical absorption and scores once in new life");
            Check(save.SequenceEqual(File.ReadAllBytes("Assets/PluginYourGames/Editor/SavesEditorYG2.json")), "personal save byte-identical");
        }
        catch (Exception error) { failure = error.ToString(); }
        finally
        {
            File.WriteAllText("Docs/QualityPass-20261108/Checks/contacts-" + label + ".json",
                Newtonsoft.Json.JsonConvert.SerializeObject(new { scope = "Editor controlled real physical contacts, not gameplay", checks = Checks, failure }, Newtonsoft.Json.Formatting.Indented));
        }
        if (failure != null) throw new Exception(failure);
        return "PASS " + Checks.Count + " physical contact checks (" + label + ").";
    }
}
