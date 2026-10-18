using System;
using System.Collections;
using UnityEngine;

namespace OldTech
{
    public sealed class DeliveryEnding : MonoBehaviour
    {
        public Transform robot;
        public WorkerVisual worker;
        public Sprite[] fireFrames;
        public bool ResultVisible { get; private set; }
        public bool RobotBurned { get; private set; }
        private HaulSession session;
        private HaulCamera view;
        private Transform boss, carryRig, fire;
        private RollingLog featuredLog;
        private Vector3 firePosition;
        private Light fireLight;
        private bool burning;
        private SoundLoop fireSound;

        public void Begin(HaulSession owner)
        {
            session = owner;
            view = FindObjectOfType<HaulCamera>();
            if (worker == null) worker = FindObjectOfType<WorkerVisual>();
            if (robot == null && owner.robot != null) robot = owner.robot.transform;
            if (robot == null) robot = owner.cargo.transform.Find("Robot");
            if (robot == null || worker == null) throw new InvalidOperationException("Ending needs the existing robot group and worker visual.");
            FreezeAtPhysicsPose(owner.cargo.Body);
            if (owner.robot != null) FreezeAtPhysicsPose(owner.robot.GetComponent<Rigidbody>());
            owner.cargo.enabled = false;
            worker.PrepareForEnding();
            foreach (RollingLog log in owner.Checkpoints.Logs)
            {
                FreezeAtPhysicsPose(log.GetComponent<Rigidbody>());
                if (featuredLog == null || Mathf.Abs(log.transform.position.x - owner.cargo.transform.position.x) <
                    Mathf.Abs(featuredLog.transform.position.x - owner.cargo.transform.position.x)) featuredLog = log;
            }
            firePosition = new Vector3(owner.Settings.finishX + 5f, 0f, 0f);
            boss = Instantiate(worker.gameObject, new Vector3(firePosition.x + 1.3f, 0f, 0f), Quaternion.identity).transform;
            boss.name = "Boss";
            boss.localScale = new Vector3(-1.1f, 1.1f, 1.1f);
            boss.GetComponent<WorkerVisual>().PrepareForEnding();
            Transform bossTorso = boss.GetComponent<WorkerVisual>().torso;
            bossTorso.localRotation = Quaternion.identity;
            Transform hair = bossTorso.Find("Hair");
            if (hair != null)
            {
                hair.localScale = new Vector3(0.72f, 0.12f, 0.68f);
                GameObject hat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                hat.name = "BossHat";
                Destroy(hat.GetComponent<Collider>());
                hat.transform.SetParent(bossTorso, false);
                hat.transform.localPosition = new Vector3(0f, 1.23f, 0f);
                hat.transform.localScale = new Vector3(0.42f, 0.28f, 0.42f);
                hat.GetComponent<Renderer>().sharedMaterial = hair.GetComponent<Renderer>().sharedMaterial;
            }
            worker.torso.localRotation = Quaternion.identity;
            MakeFire();
            StartCoroutine(PlayEnding());
        }

        private static void FreezeAtPhysicsPose(Rigidbody body)
        {
            Vector3 position = body.position;
            Quaternion rotation = body.rotation;
            body.interpolation = RigidbodyInterpolation.None;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
            body.position = position;
            body.rotation = rotation;
            body.transform.SetPositionAndRotation(position, rotation);
        }

        private IEnumerator PlayEnding()
        {
            ResultVisible = true;
            AudioFeedback.Play(Cue.Complete);
            view.Focus(new Vector3(session.Settings.finishX + 1.3f, 1.8f, 0f));
            yield return new WaitForSeconds(2f);
            ResultVisible = false;

            Vector3 center = robot.position + Vector3.up * 1.6f;
            Vector3 workerStart = worker.transform.position, bossStart = boss.position;
            yield return Animate(1.1f, t =>
            {
                worker.transform.position = Vector3.Lerp(workerStart, new Vector3(center.x - 1.3f, 0f, -1.6f), t);
                boss.position = Vector3.Lerp(bossStart, new Vector3(center.x + 1.3f, 0f, -1.6f), t);
            });

            carryRig = new GameObject("RobotCarryPivot").transform;
            carryRig.position = center;
            robot.SetParent(carryRig, true);
            Vector3 carryEnd = firePosition + new Vector3(-2.2f, 1.6f, -1.2f);
            yield return Animate(1.4f, t =>
            {
                carryRig.position = Vector3.Lerp(center, carryEnd, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.35f;
                carryRig.rotation = Quaternion.Euler(0f, 0f, t * 90f);
                worker.transform.position = new Vector3(carryRig.position.x - 1.3f, 0f, -1.6f);
                boss.position = new Vector3(carryRig.position.x + 1.3f, 0f, -1.6f);
            });
            yield return new WaitForSeconds(0.4f);
            Vector3 throwStart = carryRig.position;
            yield return Animate(0.95f, t =>
            {
                carryRig.position = Vector3.Lerp(throwStart, firePosition + Vector3.up * 0.6f, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 2.2f;
                carryRig.rotation = Quaternion.Euler(0f, 0f, 90f + t * 150f);
            });
            IgniteRobot();
            robot.gameObject.SetActive(false);
            worker.transform.position = firePosition + Vector3.left * 1.5f;
            boss.position = firePosition + Vector3.right * 1.5f;
            worker.torso.localRotation = Quaternion.Euler(0f, 0f, -12f);
            boss.GetComponent<WorkerVisual>().torso.localRotation = Quaternion.Euler(0f, 0f, -12f);
            yield return new WaitForSeconds(2.4f);
            FocusWood();
            yield return new WaitForSeconds(2f);
            session.CompleteEnding();
        }

        private static IEnumerator Animate(float duration, Action<float> draw)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                draw(t);
                yield return null;
            }
        }

        public void Skip()
        {
            StopAllCoroutines();
            ResultVisible = false;
            IgniteRobot();
            if (robot != null) robot.gameObject.SetActive(false);
            if (worker != null) worker.transform.position = firePosition + Vector3.left * 1.5f;
            if (boss != null) boss.position = firePosition + Vector3.right * 1.5f;
            FocusWood();
            session.CompleteEnding();
        }

        private void FocusWood()
        {
            if (featuredLog != null) view.Focus(featuredLog.transform.position + new Vector3(0.45f, -0.2f, 0f), 1.05f);
        }

        private void MakeFire()
        {
            fire = new GameObject("Campfire").transform;
            fire.SetPositionAndRotation(firePosition + Vector3.back * 0.6f, view.transform.rotation);
            fireSound = fire.gameObject.AddComponent<SoundLoop>();
            fireSound.Configure(Cue.Fire);
            fireSound.SetLevel(0.45f);
            SpriteRenderer drawing = fire.gameObject.AddComponent<SpriteRenderer>();
            if (fireFrames == null || fireFrames.Length != 4)
                throw new InvalidOperationException("DeliveryEnding requires the four hand-drawn fire frames.");
            drawing.sprite = fireFrames[0];
            SpriteFlipbook animation = fire.gameObject.AddComponent<SpriteFlipbook>();
            animation.frames = fireFrames;
            GameObject glow = new GameObject("Firelight");
            glow.transform.SetParent(fire, false);
            glow.transform.localPosition = new Vector3(0f, 0.8f, -0.5f);
            fireLight = glow.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.42f, 0.12f);
            fireLight.range = 9f;
            fireLight.shadows = LightShadows.None;
        }

        private void IgniteRobot()
        {
            if (burning) return;
            burning = true;
            RobotBurned = true;
            AudioFeedback.Play(Cue.FireFeed);
            if (fireSound != null) fireSound.SetLevel(1f);
        }

        private void Update()
        {
            if (fire == null) return;
            fire.localScale = Vector3.one * (burning ? 1.35f : 0.72f);
            if (fireLight != null) fireLight.intensity = (burning ? 2.5f : 1.1f) + Mathf.Sin(Time.time * 17f) * 0.16f;
        }
    }
}
