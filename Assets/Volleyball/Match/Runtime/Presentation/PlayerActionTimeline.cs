using System;
using Volleyball.Domain.Players;
using Volleyball.Domain.Simulation;
using UnityEngine;

namespace Volleyball.Presentation
{
    public sealed class PlayerActionTimeline
    {
        private ActionTimeline _contactTimeline;
        private ActionTimeline _supportTimeline;
        private TechniqueAction _supportAction;
        private bool _hasSupportAction;
        private bool _hasEmergencyReceiveWindow;
        private float _emergencyReceiveStartSimulationTime;
        private float _emergencyReceiveEndSimulationTime;
        private SimVector3 _emergencyReceiveTargetVelocity;
        private int _emergencyReceiveContactGroupId;

        internal bool SupportActionActivated { get; set; }

        public bool HasScheduledContact => _contactTimeline != null;

        public bool HasSupportAction => _hasSupportAction;

        public ActionTimeline ContactTimeline => _contactTimeline;

        public ActionTimeline SupportTimeline => _supportTimeline;

        public TechniqueAction SupportAction => _supportAction;

        public bool HasEmergencyReceiveWindow => _hasEmergencyReceiveWindow;

        public float EmergencyReceiveStartSimulationTime => _emergencyReceiveStartSimulationTime;

        public float EmergencyReceiveEndSimulationTime => _emergencyReceiveEndSimulationTime;

        public SimVector3 EmergencyReceiveTargetVelocity => _emergencyReceiveTargetVelocity;

        public int EmergencyReceiveContactGroupId => _emergencyReceiveContactGroupId;

        public void ScheduleContact(
            TechniqueAction action,
            float scheduledSimulationTime,
            float contactTimingError = 0f)
        {
            _contactTimeline = new ActionTimeline(action, scheduledSimulationTime, contactTimingError);
        }

        public void ScheduleSupport(TechniqueAction action, float scheduledSimulationTime)
        {
            _supportAction = action;
            _supportTimeline = new ActionTimeline(action, scheduledSimulationTime);
            _hasSupportAction = true;
        }

        public void ScheduleBlock(float scheduledSimulationTime)
        {
            _supportAction = TechniqueAction.Block;
            _supportTimeline = new ActionTimeline(TechniqueAction.Block, scheduledSimulationTime);
            _hasSupportAction = false;
        }

        public void CancelContact()
        {
            _contactTimeline = null;
        }

        public void DisableSupport()
        {
            _supportTimeline = null;
            _hasSupportAction = false;
        }

        public void EnableEmergencyReceive(
            float startSimulationTime,
            float endSimulationTime,
            SimVector3 targetVelocity,
            int contactGroupId)
        {
            _hasEmergencyReceiveWindow = true;
            _emergencyReceiveStartSimulationTime = startSimulationTime;
            _emergencyReceiveEndSimulationTime = Math.Max(startSimulationTime, endSimulationTime);
            _emergencyReceiveTargetVelocity = targetVelocity;
            _emergencyReceiveContactGroupId = contactGroupId;
        }

        public void DisableEmergencyReceive()
        {
            _hasEmergencyReceiveWindow = false;
        }

        public ActionTimelineSample Sample(float simulationTime)
        {
            if (_contactTimeline == null)
            {
                throw new InvalidOperationException("No contact timeline is scheduled.");
            }

            return _contactTimeline.Sample(simulationTime);
        }

        public bool TrySampleContact(float simulationTime, out ActionTimelineSample sample)
        {
            if (_contactTimeline == null)
            {
                sample = default;
                return false;
            }

            sample = _contactTimeline.Sample(simulationTime);
            return true;
        }

        public bool TrySampleSupport(float simulationTime, out ActionTimelineSample sample)
        {
            if (_supportTimeline == null)
            {
                sample = default;
                return false;
            }

            sample = _supportTimeline.Sample(simulationTime);
            return true;
        }
    }
}
