using UnityEngine;

namespace OldTech
{
    public sealed class PuzzleDirector : MonoBehaviour
    {
        public BoulderPuzzle boulder;
        public RollingLog fixedLog;
        public float lowArchStart = 143f;
        public float lowArchEnd = 149f;
        public float singlePitStart = 171f;
        public float singlePitEnd = 172.1f;
        private HaulSession session;
        private bool stoneHint, archHint, pitHint, bridgeHint;

        private void Start() { session = FindObjectOfType<HaulSession>(); }
        public bool CanAdvanceTo(float x)
        {
            if (boulder == null || x < boulder.solvedCheckpointX || boulder.Filled) return true;
            if (session != null) session.ShowHint("Fill the square pit with the boulder before continuing. Use a log to push it.");
            return false;
        }
        public void RestoreAt(float x)
        {
            if (boulder != null) boulder.Restore(x >= boulder.solvedCheckpointX);
            stoneHint = archHint = pitHint = bridgeHint = false;
            Physics.SyncTransforms();
        }
        private void Update()
        {
            if (session == null || !session.Ready || session.InputBlocked) return;
            float x = session.cargo.Body.position.x;
            if (!bridgeHint && x > 36f && x < 48f)
            {
                bridgeHint = true;
                session.ShowHint("FILL THE PIT WITH LOGS / One log is fixed in place. Add your logs so the worker can cross too.");
            }
            if (!stoneHint && x > 82f && x < 104f)
            {
                stoneHint = true;
                session.ShowHint("HEAVY STONE / Use a log as a ram. Push the stone into the square pit ahead.");
            }
            if (!archHint && x > lowArchStart - 8f && x < lowArchEnd)
            {
                archHint = true;
                session.ShowHint("LOW ARCH / Use logs to tip the robot sideways. You can lift spare logs OVER the arch.");
            }
            if (!pitHint && x > singlePitStart - 7f && x < singlePitEnd)
            {
                pitHint = true;
                session.ShowHint("ONE LOG PIT / Drop one log into the notch so the worker can cross.");
            }
        }
    }
}
