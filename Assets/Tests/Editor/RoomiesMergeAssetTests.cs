using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Asset integration checks use reflection because game scripts remain in Assembly-CSharp.
public class RoomiesMergeAssetTests
{
    private const string GameRoomPath = "Assets/_Scenes/GameRoom.unity";
    private const string PlayerPath = "Assets/_Prefabs/Player.prefab";

    [Test]
    public void GameRoomLoadsWithoutMissingScripts()
    {
        WithGameRoom(scene =>
        {
            var roots = scene.GetRootGameObjects();
            Assert.That(roots, Is.Not.Empty, "GameRoom must contain its authored objects.");
            foreach (var root in roots)
                AssertNoMissingScripts(root);
        });
    }

    [Test]
    public void PlayerPrefabHasNoMissingScriptsAndIncludesMiningPlayer()
    {
        var player = LoadPlayer();
        AssertNoMissingScripts(player);
        var miningType = Type.GetType("MiningPlayer, Assembly-CSharp", true);
        Assert.That(player.GetComponent(miningType), Is.Not.Null,
            "The animation prefab must retain the mining NetworkBehaviour.");
    }

    [Test]
    public void PlayerAnimatorHasValidAvatarAndAnimationController()
    {
        var player = LoadPlayer();
        var movementType = Type.GetType("PlayerMovement, Assembly-CSharp", true);
        var movement = player.GetComponent(movementType);
        Assert.That(movement, Is.Not.Null);

        // PlayerMovement permits an empty serialized field and resolves a child Animator at startup.
        var animatorField = new SerializedObject(movement).FindProperty("animator");
        Assert.That(animatorField, Is.Not.Null);
        var animator = animatorField.objectReferenceValue as Animator;
        if (animator == null) animator = player.GetComponentInChildren<Animator>(true);

        Assert.That(animator, Is.Not.Null, "PlayerMovement must be able to resolve an Animator.");
        Assert.That(animator.avatar, Is.Not.Null, "The imported player avatar must resolve.");
        Assert.That(animator.avatar.isValid, Is.True, "The player avatar import must be valid.");
        Assert.That(animator.runtimeAnimatorController, Is.Not.Null,
            "The merged animation controller must be assigned.");
        var clips = animator.runtimeAnimatorController.animationClips;
        Assert.That(clips, Is.Not.Empty, "The controller must resolve its animation clips.");
        foreach (var clip in clips)
            Assert.That(clip, Is.Not.Null, "A controller animation reference is missing.");
    }

    [Test]
    public void MiningSceneConfigurationPassesExistingValidation()
    {
        WithGameRoom(scene =>
        {
            var builder = Type.GetType("RoomiesMiningBuilder, Assembly-CSharp-Editor", true);
            var validate = builder.GetMethod("Validate", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(Scene) }, null);
            Assert.That(validate, Is.Not.Null);
            try
            {
                // Validate checks references and physics only. It does not rebuild, polish or render.
                validate.Invoke(null, new object[] { scene });
            }
            catch (TargetInvocationException exception)
            {
                Assert.Fail((exception.InnerException ?? exception).ToString());
            }
        });
    }

    private static GameObject LoadPlayer()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
        Assert.That(player, Is.Not.Null, "Player.prefab must import successfully.");
        return player;
    }

    private static void AssertNoMissingScripts(GameObject root)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
        {
            Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject),
                Is.Zero, "Missing script on " + HierarchyPath(child));
        }
    }

    private static string HierarchyPath(Transform child)
    {
        string path = child.name;
        while (child.parent != null)
        {
            child = child.parent;
            path = child.name + "/" + path;
        }
        return path;
    }

    private static void WithGameRoom(Action<Scene> check)
    {
        Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False,
            "Run asset integration checks outside Play Mode.");
        var scene = SceneManager.GetSceneByPath(GameRoomPath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(GameRoomPath, OpenSceneMode.Additive);
        try
        {
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True, "GameRoom must load.");
            check(scene);
        }
        finally
        {
            if (openedHere && scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
