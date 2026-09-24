using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

// Reflection preserves the project's existing Assembly-CSharp layout.
public class RoomiesAnimationMergeTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);

    private static void InvokeLifecycle(Component component, string method)
    {
        var callback = component.GetType().GetMethod(method, PrivateInstance | BindingFlags.Public);
        Assert.That(callback, Is.Not.Null, method);
        callback.Invoke(component, null);
    }

    [Test]
    public void NonOwnerPlayerResolvesAnimatorBeforeInputIsDisabledAndHasMatchingParameters()
    {
        // Prefab contents give us the real serialized setup without starting a game session.
        var player = PrefabUtility.LoadPrefabContents("Assets/_Prefabs/Player.prefab");
        try
        {
            var movement = (NetworkBehaviour)player.GetComponent(GameType("PlayerMovement"));
            var animator = player.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(movement.IsOwner, Is.False);

            InvokeLifecycle(movement, "Awake");
            InvokeLifecycle(movement, "Start");

            Assert.That(movement.enabled, Is.False);
            Assert.That(movement.GetType().GetField("animator", PrivateInstance).GetValue(movement),
                Is.SameAs(animator), "Remote jump/emote RPCs need an Animator even when owner input is disabled.");

            var controller = animator.runtimeAnimatorController as AnimatorController;
            Assert.That(controller, Is.Not.Null);
            var parameters = controller.parameters.Select(parameter => parameter.nameHash).ToArray();
            foreach (var field in movement.GetType().GetFields(BindingFlags.Static | BindingFlags.NonPublic)
                         .Where(field => field.FieldType == typeof(int) && field.Name.EndsWith("Hash")))
            {
                Assert.That(parameters, Does.Contain((int)field.GetValue(null)),
                    "PlayerMovement parameter missing from the prefab's controller: " + field.Name);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(player);
        }
    }

    [TestCase("OnDisable")]
    [TestCase("OnNetworkDespawn")]
    public void PlayerCleanupCancelsSelectionWithoutCreatingAnEmoteWheel(string callback)
    {
        var wheelType = GameType("EmoteWheelUI");
        var singleton = wheelType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(singleton.GetValue(null) as Object, Is.Null, "Run outside an active game session.");

        var player = new GameObject("RoomiesTest_EmoteCleanup");
        player.SetActive(false);
        try
        {
            player.AddComponent<NetworkObject>();
            var movement = (Component)player.AddComponent(GameType("PlayerMovement"));
            var selectionOpen = movement.GetType().GetField("isEmoteWheelOpen", PrivateInstance);
            selectionOpen.SetValue(movement, true);

            InvokeLifecycle(movement, callback);

            Assert.That(selectionOpen.GetValue(movement), Is.False);
            Assert.That(singleton.GetValue(null) as Object, Is.Null,
                "Cleanup must not construct persistent UI while the player is being removed.");
        }
        finally
        {
            Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void WalkingCanEnterCrouchWithoutWaitingForThePlayerToStop()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/_Animation/Bob_animation.controller");
        Assert.That(controller, Is.Not.Null);
        var states = controller.layers[0].stateMachine.states.Select(child => child.state).ToArray();
        var walk = states.Single(state => state.name == "Walk");
        var idle = states.Single(state => state.name == "Idle");
        var crouch = states.Single(state => state.name == "Crouch");

        Assert.That(walk.transitions.Any(transition => transition.destinationState == crouch
            && !transition.hasExitTime
            && transition.conditions.Length == 1
            && transition.conditions[0].parameter == "IsCrouching"
            && transition.conditions[0].mode == AnimatorConditionMode.If), Is.True);
        Assert.That(idle.transitions.Where(transition => transition.destinationState == walk)
            .All(transition => transition.conditions.Any(condition => condition.parameter == "IsCrouching"
                && condition.mode == AnimatorConditionMode.IfNot)), Is.True,
            "Movement speed must not select the standing walk animation while crouched.");
    }
}
