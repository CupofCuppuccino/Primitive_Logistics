using UnityEngine;

namespace OldTech
{
    [DefaultExecutionOrder(-100)]
    public sealed class HaulSession : MonoBehaviour
    {
        public HaulSettings defaults;
        public PhysicMaterial contactMaterial;
        public CargoMotor cargo;
        public RobotCargo robot;
        public LogGrabber grabber;
        public bool Ready { get; private set; }
        public bool Finished { get; private set; }
        public bool IsPaused { get; private set; }
        public bool TuningOpen { get; private set; }
        public bool EndingComplete { get; private set; }
        public bool InputBlocked => Finished || IsPaused || TuningOpen || (Application.isPlaying && !Ready);
        public int Impacts { get; private set; }
        public float Elapsed { get; private set; }
        public int Integrity => Mathf.Max(0, 100 - Impacts * 7);
        public HaulSettings Settings => runtimeSettings != null ? runtimeSettings : defaults;
        public CheckpointController Checkpoints { get; private set; }
        public PuzzleDirector Puzzles { get; private set; }
        public string Hint => Time.unscaledTime < hintUntil ? hint : string.Empty;
        private HaulSettings runtimeSettings;
        private PhysicMaterial runtimeContact;
        private RollingLog[] logs;
        private DeliveryEnding ending;
        private string hint;
        private float hintUntil, ignoreImpactsUntil, toppledTime;

        private void Awake()
        {
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 1f / 90f;
            Time.maximumDeltaTime = 0.1f;
            Physics.simulationMode = SimulationMode.FixedUpdate;
            runtimeSettings = Instantiate(defaults);
            runtimeSettings.name = "LiveHaulSettings";
            logs = FindObjectsOfType<RollingLog>(true);
            if (robot == null) robot = FindObjectOfType<RobotCargo>();
            Puzzles = FindObjectOfType<PuzzleDirector>();
            runtimeContact = Instantiate(contactMaterial);
            foreach (Collider item in FindObjectsOfType<Collider>())
                if (item.sharedMaterial == contactMaterial) item.sharedMaterial = runtimeContact;
            Checkpoints = GetComponent<CheckpointController>();
            if (Checkpoints == null) Checkpoints = gameObject.AddComponent<CheckpointController>();
            if (GetComponent<HaulHud>() == null) gameObject.AddComponent<HaulHud>();
            if (GetComponent<HaulSoundscape>() == null) gameObject.AddComponent<HaulSoundscape>();
            ending = GetComponent<DeliveryEnding>();
            if (ending == null) ending = gameObject.AddComponent<DeliveryEnding>();
            ApplyTuning();
        }

        private void Start()
        {
            Checkpoints.Initialize(this);
            if (!GameProgress.TryLoad(out DeliverySave save) || save.completed)
            {
                save = new DeliverySave();
                GameProgress.Save(save);
            }
            Impacts = save.impacts;
            Elapsed = save.elapsed;
            Checkpoints.Restore(save.checkpointIndex);
            ignoreImpactsUntil = Time.time + 1.5f;
            Ready = true;
            ShowHint(Checkpoints.CurrentIndex == 0 ? "Three logs. Collect the spare before the first pit. Keep the robot on board!" : "Delivery resumed. " + Checkpoints.OwnedLogCount + " logs recovered.");
        }

        private void Update()
        {
            if (!Ready) return;
            if (Input.GetKeyDown(KeyCode.Escape) && !Finished) TogglePause();
            if (DeveloperMode.IsUnlocked && Input.GetKeyDown(KeyCode.F1) && !Finished) ToggleTuning();
            if (Input.GetKeyDown(KeyCode.R) && !Finished) RestoreCheckpoint();
            if (DeveloperMode.IsUnlocked && !Finished)
                for (int index = 0; index <= Mathf.Min(9, Settings.checkpointPositions.Length); index++)
                    if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + index)) ||
                        Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad0 + index)))
                    {
                        JumpToCheckpoint(index);
                        return;
                    }
            if (InputBlocked) return;
            Elapsed += Time.deltaTime;
            Checkpoints.TryAdvance();
            if (cargo.Body.position.x >= Settings.finishX && (robot == null || robot.IsOnPallet) &&
                (Puzzles == null || Puzzles.CanAdvanceTo(Settings.finishX)))
            {
                Finished = true;
                grabber.Release();
                ending.Begin(this);
                return;
            }
            float tilt = Mathf.Abs(Mathf.DeltaAngle(0f, cargo.Body.rotation.eulerAngles.z));
            toppledTime = tilt > 80f && cargo.Body.velocity.sqrMagnitude < 0.6f ? toppledTime + Time.deltaTime : 0f;
            if (cargo.Body.position.y < -3f || toppledTime > 2.5f) RestoreCheckpoint();
            if (robot != null && (robot.Body.position.y < -4f || Mathf.Abs(robot.Body.position.x - cargo.Body.position.x) > 14f)) RestoreCheckpoint();
            else if (robot != null && !robot.IsOnPallet) ShowHint("ROBOT OFF THE PALLET! R restores the robot, pallet and this checkpoint's logs.");
        }

        public void CheckpointReached()
        {
            GameProgress.Save(new DeliverySave
            {
                checkpointIndex = Checkpoints.CurrentIndex,
                impacts = Impacts,
                elapsed = Elapsed
            });
            ShowHint("CHECKPOINT " + Checkpoints.CurrentIndex + " SAVED  /  R restores robot + " + Checkpoints.RecoveryLogCount + " logs");
            AudioFeedback.Play(Cue.Checkpoint);
        }

        public void RestoreCheckpoint()
        {
            if (!Ready || Finished) return;
            grabber.Release();
            IsPaused = false;
            TuningOpen = false;
            Time.timeScale = 1f;
            if (GameProgress.TryLoad(out DeliverySave save))
            {
                Impacts = save.impacts;
                Elapsed = save.elapsed;
            }
            Checkpoints.Restore(Checkpoints.CurrentIndex);
            ignoreImpactsUntil = Time.time + 1.2f;
            toppledTime = 0f;
            ShowHint("Checkpoint restored. " + Checkpoints.OwnedLogCount + " logs recovered beneath the pallet.");
            AudioFeedback.Play(Cue.Recover);
        }

        public void JumpToCheckpoint(int index)
        {
            if (!DeveloperMode.IsUnlocked || !Ready || Finished || index < 0 || index > Settings.checkpointPositions.Length) return;
            grabber.Release();
            IsPaused = false;
            TuningOpen = false;
            Time.timeScale = 1f;
            Checkpoints.Restore(index);
            ignoreImpactsUntil = Time.time + 1.2f;
            toppledTime = 0f;
            // Make this the retry/continue destination, including when jumping backwards.
            GameProgress.Save(new DeliverySave { checkpointIndex = index, impacts = Impacts, elapsed = Elapsed });
            DaylightCycle daylight = FindObjectOfType<DaylightCycle>();
            if (daylight != null) daylight.ApplyProgress(cargo.Body.position.x / Mathf.Max(1f, Settings.finishX));
            ShowHint(index == 0 ? "BACK TO START / Three logs recovered." :
                "JUMPED TO CHECKPOINT " + index + " / " + Checkpoints.OwnedLogCount + " logs recovered. R retries here.");
            AudioFeedback.Play(Cue.Recover);
        }

        public void RecordImpact()
        {
            if (InputBlocked || Time.time < ignoreImpactsUntil) return;
            Impacts++;
            ShowHint("Cargo hit! Keep both ends supported.");
            AudioFeedback.Play(Cue.Impact);
        }

        public void ShowHint(string message)
        {
            hint = message;
            hintUntil = Time.unscaledTime + 4f;
        }

        public void TogglePause()
        {
            if (Finished) return;
            grabber.Release();
            TuningOpen = false;
            IsPaused = !IsPaused;
            Time.timeScale = IsPaused ? 0f : 1f;
        }

        public void ToggleTuning()
        {
            if (!DeveloperMode.IsUnlocked || !Ready || Finished) return;
            grabber.Release();
            IsPaused = false;
            TuningOpen = !TuningOpen;
            Time.timeScale = TuningOpen ? 0f : 1f;
        }

        public void CompleteEnding()
        {
            EndingComplete = true;
            GameProgress.CompleteDelivery();
        }

        public void ReturnToMenu()
        {
            grabber.Release();
            GameScenes.OpenMenu();
        }

        public void RetryLastCheckpoint()
        {
            GameProgress.ReopenLastCheckpoint();
            GameScenes.OpenDelivery();
        }

        public void ApplyTuning()
        {
            if (runtimeContact != null)
            {
                runtimeContact.dynamicFriction = Settings.friction;
                runtimeContact.staticFriction = Settings.friction;
            }
            if (logs == null) return;
            foreach (RollingLog log in logs) if (log != null) log.ApplyTuning();
        }

        private void OnDestroy()
        {
            Time.timeScale = 1f;
            if (runtimeSettings != null) Destroy(runtimeSettings);
            if (runtimeContact != null) Destroy(runtimeContact);
        }
    }
}
