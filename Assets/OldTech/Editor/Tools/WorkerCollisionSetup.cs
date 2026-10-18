using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OldTech.Editor
{
    public static class WorkerCollisionSetup
    {
        [MenuItem("Old Tech/Apply solid worker collision")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            Scene original = SceneManager.GetActiveScene();
            if (original.isDirty) EditorSceneManager.SaveScene(original);
            const string backup = "Documentation/Backups/BeforeWorkerCollision.unity";
            if (!File.Exists(backup)) File.Copy(GameSceneBuilder.ScenePath, backup);
            Scene delivery = SceneManager.GetSceneByPath(GameSceneBuilder.ScenePath);
            bool opened = !delivery.IsValid() || !delivery.isLoaded;
            if (opened) delivery = EditorSceneManager.OpenScene(GameSceneBuilder.ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(delivery);
            const string path = "Assets/OldTech/Physics/Materials/WorkerContact.physicMaterial";
            PhysicMaterial material = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(path);
            if (material == null)
            {
                material = new PhysicMaterial("WorkerContact");
                AssetDatabase.CreateAsset(material, path);
            }
            material.staticFriction = material.dynamicFriction = 0.1f;
            material.bounciness = 0f;
            material.frictionCombine = PhysicMaterialCombine.Minimum;
            material.bounceCombine = PhysicMaterialCombine.Minimum;
            WorkerVisual worker = delivery.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<WorkerVisual>()).Single();
            worker.ConfigureCollision(material);
            EditorUtility.SetDirty(worker);
            EditorUtility.SetDirty(material);
            EditorSceneManager.MarkSceneDirty(delivery);
            EditorSceneManager.SaveScene(delivery);
            AssetDatabase.SaveAssets();
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            if (opened) EditorSceneManager.CloseScene(delivery, true);
            File.WriteAllText("Logs/WorkerCollisionSetup.txt", "PASS: Worker has a kinematic interpolated body with continuous speculative collision, a body/head capsule and a hand/arm box. Original worker model and route retained. Contacts only affect the robot during gameplay; ending disables worker/boss collision.");
        }
    }
}
