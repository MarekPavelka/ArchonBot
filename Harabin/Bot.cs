namespace Harabin
{
    using BroodWar.Api;
    using NBWTA.Result;
    using NBWTA.Utils;
    using SmakenziBot.Utils;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnitType = BroodWar.Api.Enum.UnitType;
    using UpgradeType = BroodWar.Api.Enum.UpgradeType;

    class Bot
    {
        private AnalyzedMap _analyzedMap;
        private const int MaxWorkerCount = 17;

        public void OnGameStart()
        {
            _analyzedMap = TerrainAnalyzerAdapter.Get(); // chokes, regions, expands
            Game.SetLocalSpeed(1); // speeds up the game
            //Game.SetFrameSkip(2); speeds up the game
            Game.SetCommandOptimizationLevel(1); // makes a group of 12 before sending commands
        }

        public void OnFrame()
        {
            OrderAllIdleWorkersToGatherClosestMineral();

            if (MyWorkers().Count <= MaxWorkerCount && Game.Self.Minerals >= 50)
                TrainProbe();

            if (MyWorkers().Count > 7 && Game.Self.Minerals >= 100 && !MyBuildingsOfType(UnitType.Protoss_Pylon).Any())
                Build(UnitType.Protoss_Pylon);

            if (HaveFinishedBuilding(UnitType.Protoss_Pylon) && Game.Self.Minerals >= 150 && !MyBuildingsOfType(UnitType.Protoss_Gateway).Any())
                Build(UnitType.Protoss_Gateway);

            if (MyBuildingsOfType(UnitType.Protoss_Gateway).Any() && Game.Self.Minerals >= 100)
                Build(UnitType.Protoss_Assimilator);

            var myUnitsGatheringGas = Game.Self.Units.Where(u => u.IsGatheringGas);
            if (HaveFinishedBuilding(UnitType.Protoss_Assimilator) && myUnitsGatheringGas.Count() < 3) SendOneProbeFromMineralToGas();
            //if (myUnitsGatheringGas.Count() > 3) SendOneProbeFromGasToMineral();

            if (Game.CanMake(UnitType.Protoss_Cybernetics_Core, null) && !MyBuildingsOfType(UnitType.Protoss_Cybernetics_Core).Any())
                Build(UnitType.Protoss_Cybernetics_Core);

            if (Game.CanUpgrade(UpgradeType.Singularity_Charge, null, false))
            {
                var upgradeType = Upgrade.AllUpgrades.First(u => u.Type == UpgradeType.Singularity_Charge);
                MyBuildingsOfType(UnitType.Protoss_Cybernetics_Core).First().PerformUpgrade(upgradeType);
            }

            if (MyWorkers().Count >= MaxWorkerCount && AvailableSupply() < 3 && Game.CanMake(UnitType.Protoss_Pylon, null) && !IsBeingConstructed(UnitType.Protoss_Pylon))
                Build(UnitType.Protoss_Pylon);

            if (Game.CanMake(UnitType.Protoss_Dragoon, null)) TrainDragoon();

            var myFightersInBase = MyFighters().Where(f => IsInBase(f)).Where(f => f.IsIdle).ToList();

            // send dragon to nearest chokepoint _analyzedMap.ChokeRegions.Where(region => Unit unit.TilePosition.CalcApproximateDistance  Game.Self.StartLocation)

            // move fighters into chokepoint from nbwta map analyzer if (myFightersInBase.Count > 0) myFightersInBase.ForEach(f => f.Move());


            if (myFightersInBase.Count >= 12) ShiftAttackAllStartLocations(myFightersInBase);

            if (Game.CanMake(UnitType.Protoss_Photon_Cannon, null) &&
                !MyBuildingsOfType(UnitType.Protoss_Photon_Cannon).Any())
                Build(UnitType.Protoss_Photon_Cannon);
            if (Game.CanMake(UnitType.Protoss_Citadel_of_Adun, null) && !MyBuildingsOfType(UnitType.Protoss_Citadel_of_Adun).Any())
                Build(UnitType.Protoss_Citadel_of_Adun);

            if (Game.CanUpgrade(UpgradeType.Leg_Enhancements, null, false))
            {
                var upgradeType = Upgrade.AllUpgrades.First(u => u.Type == UpgradeType.Leg_Enhancements);
                MyBuildingsOfType(UnitType.Protoss_Citadel_of_Adun).First().PerformUpgrade(upgradeType);
            }

            if (MyBuildingsOfType(UnitType.Protoss_Gateway).Count < 5) Build(UnitType.Protoss_Gateway);

            if(MyBuildingsOfType(UnitType.Protoss_Gateway).Count >= 5)
                foreach (var gateway in MyBuildingsOfType(UnitType.Protoss_Gateway))
                {
                    if (!gateway.TrainingQueue.Any()) gateway.Train(UnitType.Protoss_Zealot);
                }
        }

        private static void ShiftAttackAllStartLocations(List<Unit> myFighters)
        {
            var notMyBaseStartLocations = Game.StartLocations.Where(sl => sl != Game.Self.StartLocation) 
                .Select(loc => new Position(loc.X * 32, loc.Y * 32)) // TilePosition (build tile) -> Position  1:32
                .ToList();

            myFighters.ForEach(x => notMyBaseStartLocations.ForEach(position => x.Attack(position, true)));  
        }

        void OrderAllIdleWorkersToGatherClosestMineral()
        {
            //Game.Self.Units.Where(u => u.UnitType.IsWorker).Where(w => w.IsIdle).ForEach(w => GatherClosestMineral(w));

            foreach (var myWorker in MyWorkers())
                if (myWorker.IsIdle) GatherClosestMineral(myWorker);
        }

        void GatherClosestMineral(Unit worker)
        {
            var closestMineral = BaseMinerals().OrderBy(m => worker.Distance(m)).First();
            worker.Gather(closestMineral, false);
        }

        private void SendOneProbeFromMineralToGas()
        {
            var freeMineralWorkers = MyWorkers().Where(w => w.IsGatheringMinerals && !w.IsCarryingMinerals).ToList();
            if (!freeMineralWorkers.Any()) return;
            GatherGas(freeMineralWorkers.First());
        }

        private void GatherGas(Unit worker)
        {
            var gas = MyBuildingsOfType(UnitType.Protoss_Assimilator).First();
            worker.Gather(gas, false);
        }

        private void SendOneProbeFromGasToMineral()
        {
            var freeGasWorker = MyWorkers().Where(w => w.IsGatheringGas && !w.IsCarryingGas).First();
            GatherClosestMineral(freeGasWorker);
        }

        List<Unit> BaseMinerals()
        {
            return Game.Minerals.Where(IsInBase).ToList();
        }

        bool IsInBase(Unit unit)
        {
            return unit.TilePosition.CalcApproximateDistance(Game.Self.StartLocation) < 15;
        }

        private List<Unit> MyWorkers()
        {
            return Game.Self.Units.Where(u => u.UnitType.IsWorker).ToList();
        }

        private List<Unit> MyFighters()
        {
            return Game.Self.Units.Where(u => u.UnitType.CanAttack && !u.UnitType.IsWorker).ToList();
        }

        private List<Unit> MyBuildingsOfType(UnitType type)
        {
            return Game.Self.Units.Where(u => u.UnitType.Type == type).ToList();
        }

        private bool HaveFinishedBuilding(UnitType buildingType)
        {
            return MyBuildingsOfType(buildingType).Where(p => !p.IsBeingConstructed).Any();
        }

        private bool IsBeingConstructed(UnitType buildingType)
        {
            return MyBuildingsOfType(buildingType).Where(p => p.IsBeingConstructed).Any();
        }

        private void TrainProbe()
        {
            var nexus = Game.Self.Units.Where(u => u.UnitType.Type == UnitType.Protoss_Nexus).First();
            if (!nexus.TrainingQueue.Any()) nexus.Train(UnitType.Protoss_Probe);
        }

        private void TrainDragoon()
        {
            var gateway = Game.Self.Units.Where(u => u.UnitType.Type == UnitType.Protoss_Gateway).First();
            if (!gateway.TrainingQueue.Any()) gateway.Train(UnitType.Protoss_Dragoon);
        }

        private void Build(UnitType whatToBuild)
        {
            var freeProbe = MyWorkers().Where(w => !w.IsCarryingMinerals).First();
            var buildingType = BroodWar.Api.UnitType.AllUnitTypes.First(t => t.Type == whatToBuild);
            var buildLocation = Game.GetBuildLocation(buildingType, Game.Self.StartLocation, 64, false);
            freeProbe.Build(whatToBuild, buildLocation);
        }

        private int AvailableSupply()
        {
            return (Game.Self.SupplyTotal - Game.Self.SupplyUsed) / 2;
        }
    }
}
