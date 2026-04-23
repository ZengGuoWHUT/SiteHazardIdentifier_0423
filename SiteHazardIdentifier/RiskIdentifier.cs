using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RevitVoxelzation;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.OleDb;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Media.Media3D;
using Material = Autodesk.Revit.DB.Material;
namespace SiteHazardIdentifier
{
    public class RiskIdentifier
    {
        private string endpoint;
        private string apiKey;
        private string modelName;
        public LLMClient Client { get; private set; }
        private string systemMsg;
        public Dictionary<string, Work> WorkMap { get; set; }

        public Dictionary<string, HazardVoxelElementInfo> ElemVoxRel { get; set; }
        public Dictionary<string, HazardMeshElementInfo> ElemMeshRel { get; set; }
        public Dictionary<string, HazardBoxElementInfo> ElemBoxRel { get; set; }

        public Dictionary<string, HazardMaterial> MaterialMap { get; set; } = new Dictionary<string, HazardMaterial>();
        public List<Work> Works { get; set; }
        public double VoxelSize { get; set; }
        public double FFHBuffer { get; set; } = 2000;

        public double FFHHeight { get; set; } = 2000;
        public double FFHProtectionHeight { get; set; } = 1100;
        public RiskIdentifier()
        {

        }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="voxelElements">voxel elements</param>
        /// <param name="works">works</param>
        /// <param name="voxelSize">voxel sizes</param>
        public RiskIdentifier(List<VoxelElement> voxelElements, List<Work> works, double voxelSize)
        {

            this.Works = works.OrderBy(c => c.Get_Start()).ToList();
            this.VoxelSize = voxelSize;
            //map work and elementid
            var ElemWorkRel = new Dictionary<string, List<Work>>();
            WorkMap = new Dictionary<string, Work>();
            foreach (var work in works)
            {
                WorkMap.Add(work.Id, work);
                foreach (var elemId in work.ElementIds)
                {
                    if (!ElemWorkRel.ContainsKey(elemId))
                    {
                        ElemWorkRel.Add(elemId, new List<Work>());
                    }
                    ElemWorkRel[elemId].Add(work);
                }
            }
            //map elemId and voxels
            Dictionary<string, HazardVoxelElementInfo> elemeId_voxes = new Dictionary<string, HazardVoxelElementInfo>();
            foreach (var ve in voxelElements)
            {
                if (ElemWorkRel.ContainsKey(ve.ElementId))
                    elemeId_voxes.Add(ve.ElementId, new HazardVoxelElementInfo(ve, ElemWorkRel[ve.ElementId]));
            }

            this.ElemVoxRel = elemeId_voxes;
        }


        public RiskIdentifier(List<MeshElement> meshes, List<Work> works, double voxelSize)
        {

            this.Works = works.OrderBy(c => c.Get_Start()).ToList();
            this.VoxelSize = voxelSize;
            //map work and elementid
            var ElemWorkRel = new Dictionary<string, List<Work>>();
            WorkMap = new Dictionary<string, Work>();
            foreach (var work in works)
            {
                WorkMap.Add(work.Id, work);
                foreach (var elemId in work.ElementIds)
                {
                    if (!ElemWorkRel.ContainsKey(elemId))
                    {
                        ElemWorkRel.Add(elemId, new List<Work>());
                    }
                    ElemWorkRel[elemId].Add(work);
                }
            }
            //map elemId and voxels
            Dictionary<string, HazardMeshElementInfo> elemeId_meshes = new Dictionary<string, HazardMeshElementInfo>();
            foreach (var ve in meshes)
            {
                if (ElemWorkRel.ContainsKey(ve.ElementId))
                    elemeId_meshes.Add(ve.ElementId, new HazardMeshElementInfo(ve, ElemWorkRel[ve.ElementId]));
            }
            this.ElemMeshRel = elemeId_meshes;
        }

        public RiskIdentifier(List<LightWeightVoxelElement> boxElems, List<Work> works, double voxelSize)
        {

            this.Works = works.OrderBy(c => c.Get_Start()).ToList();
            this.VoxelSize = voxelSize;
            //map work and elementid
            var ElemWorkRel = new Dictionary<string, List<Work>>();
            WorkMap = new Dictionary<string, Work>();
            foreach (var work in works)
            {
                WorkMap.Add(work.Id, work);
                foreach (var elemId in work.ElementIds)
                {
                    if (!ElemWorkRel.ContainsKey(elemId))
                    {
                        ElemWorkRel.Add(elemId, new List<Work>());
                    }
                    ElemWorkRel[elemId].Add(work);
                }
            }
            //map elemId and voxels
            Dictionary<string, HazardBoxElementInfo> elemeId_boxes = new Dictionary<string, HazardBoxElementInfo>();
            foreach (var ve in boxElems)
            {
                if (ElemWorkRel.ContainsKey(ve.ElementId))
                    elemeId_boxes.Add(ve.ElementId, new HazardBoxElementInfo(ve, ElemWorkRel[ve.ElementId], (int)this.VoxelSize));
            }
            this.ElemBoxRel = elemeId_boxes;
        }

        public IEnumerable<_4DElement> IternateElements()
        {
            if (this.ElemVoxRel != null)
            {
                foreach (var ve in this.ElemVoxRel.Values)
                    yield return ve;
            }
            else if (this.ElemMeshRel != null)
            {
                foreach (var m in this.ElemMeshRel.Values)
                    yield return m;
            }
            else
            {
                foreach (var m in this.ElemBoxRel.Values)
                    yield return m;
            }

        }

        /// <summary>
        /// Generate Chunks for a given work
        /// </summary>
        /// <param name="currentWork">Current work</param>
        /// <param name="defaultFireRange">Fire protection range</param>
        /// <param name="timeBuffer">time buffer considering the remaining of flamable gas</param>
        /// <param name="elemId_InternalId">elem-mat Id</param>
        /// <returns>chunks </returns>
        public void IdentifyGlobalFireHazard_Voxel(double defaultFireRange, Dictionary<string, string> elemId_InternalId, IProgress<(int, string)> progress)
        {
            //update elem index
            int i = 0;
            foreach (var elem in this.ElemVoxRel.Values)
            {
                elem.Combinations = new List<HazardCombination>();
                elem.UpdateIndex(i);
                i += 1;
            }
            var elemTemp = this.ElemVoxRel.Values.ToList();
            var result = new List<HazardCombination>();
            //expand voxels 2D
            Dictionary<CellIndex, List<int>> cix_ElemIdx = new Dictionary<CellIndex, List<int>>();
            foreach (var elem in this.ElemVoxRel.Values)
            {
                foreach (var vox in elem.Voxels)
                {
                    var cix = new CellIndex(vox.ColIndex, vox.RowIndex);
                    if (cix_ElemIdx.TryGetValue(cix, out var elemIds))
                    {
                        elemIds.Add(elem.GetIndex());
                    }
                    else
                    {
                        cix_ElemIdx.Add(cix, new List<int>() { elem.GetIndex() });
                    }
                }
            }
            //get combustible elements
            List<Work> igntionWorks = new List<Work>();
            foreach (var work in this.Works)
            {
                if (work.EmitSparks)
                {
                    igntionWorks.Add(work);
                }
            }
            bool[,] elemIdx_FireWorkIdx = new bool[this.ElemVoxRel.Values.Count, igntionWorks.Count];
            for (int fire = 0; fire <= igntionWorks.Count - 1; fire++)
            {
                var workFire = igntionWorks[fire];
                List<HazardVoxelElementInfo> ignitionElems = new List<HazardVoxelElementInfo>();
                foreach (var elemId in workFire.ElementIds)
                {
                    if (this.ElemVoxRel.TryGetValue(elemId, out var elem))
                    {
                        ignitionElems.Add(elem);
                    }
                }
                //get fire separation range
                var ignitionRange2D = this.GetFireSeparationRange2(ignitionElems, defaultFireRange);
                //separation fire
                foreach (var cixFire in ignitionRange2D)
                {
                    if (cix_ElemIdx.TryGetValue(cixFire, out var elemIdx))
                    {
                        foreach (var eidx in elemIdx)
                        {
                            elemIdx_FireWorkIdx[eidx, fire] = true;
                        }
                    }
                }
                progress.Report(((int)Math.Round((double)fire / igntionWorks.Count * 100), $"{fire + 1} Ignition work processes, total:{igntionWorks.Count}"));
            }
            //crereaete combo
            int numElems = elemIdx_FireWorkIdx.GetUpperBound(0) + 1;
            for (int elemId = 0; elemId <= numElems - 1; elemId++)
            {
                var elem = elemTemp[elemId];
                bool elemUnderFire = false;
                for (int fire = 0; fire <= elemIdx_FireWorkIdx.GetUpperBound(1); fire++)
                {
                    if (elemIdx_FireWorkIdx[elemId, fire] == true)
                    {
                        var workFire = igntionWorks[fire];
                        if (!elem.IsElementVoidDuringWork(workFire))
                        {
                            elemUnderFire = true;
                            var comb = new HazardCombination(elem, workFire);
                            elem.Combinations.Add(comb);
                        }
                    }
                }
                if (elemUnderFire == false)
                {
                    var comb = new HazardCombination(elem);
                    elem.Combinations.Add(comb);
                }
                int percentage = (int)Math.Round((double)(elemId + 1) / numElems * 100);
                if (percentage % 10 == 0)
                    progress.Report((percentage, $"{elemId + 1} elements scanned, total:{numElems}"));
            }
        }

        /// <summary>
        /// Generate Chunks for a given work
        /// </summary>
        /// <param name="currentWork">Current work</param>
        /// <param name="defaultFireRange">Fire protection range</param>
        /// <param name="timeBuffer">time buffer considering the remaining of flamable gas</param>
        /// <param name="elemId_InternalId">elem-mat Id</param>
        /// <returns>chunks </returns>
        public void IdentifyGlobalFireHazard_VoxelBox2(double defaultFireRange, Dictionary<string, string> elemId_InternalId, IProgress<(int, string)> progress)
        {
            //update elem index
            int i = 0;
            foreach (var elem in this.ElemBoxRel.Values)
            {
                elem.Combinations = new List<HazardCombination>();
                elem.UpdateIndex(i);
                i += 1;
            }
            var elemTemp = this.ElemBoxRel.Values.ToList();
            var result = new List<HazardCombination>();
            //expand voxels 2D
            Dictionary<CellIndex, List<int>> cix_ElemIdx = new Dictionary<CellIndex, List<int>>();
            foreach (var elem in this.ElemBoxRel.Values)
            {
                foreach (var cix in elem.Get2DProjection((int)this.VoxelSize))
                {
                    if (cix_ElemIdx.TryGetValue(cix, out var elemIds))
                    {
                        elemIds.Add(elem.GetIndex());
                    }
                    else
                    {
                        cix_ElemIdx.Add(cix, new List<int>() { elem.GetIndex() });
                    }
                }
            }
            //get combustible elements
            List<Work> igntionWorks = new List<Work>();
            foreach (var work in this.Works)
            {
                if (work.EmitSparks)
                {
                    igntionWorks.Add(work);
                }
            }
            bool[,] elemIdx_FireWorkIdx = new bool[this.ElemBoxRel.Values.Count, igntionWorks.Count];
            for (int fire = 0; fire <= igntionWorks.Count - 1; fire++)
            {
                var workFire = igntionWorks[fire];
                List<HazardBoxElementInfo> ignitionElems = new List<HazardBoxElementInfo>();
                foreach (var elemId in workFire.ElementIds)
                {
                    if (this.ElemBoxRel.TryGetValue(elemId, out var elem))
                    {
                        ignitionElems.Add(elem);
                    }
                }
                //get fire separation range
                var ignitionRange2D = this.GetFireSeparationRange2(ignitionElems, defaultFireRange);
                //separation fire
                foreach (var cixFire in ignitionRange2D)
                {
                    if (cix_ElemIdx.TryGetValue(cixFire, out var elemIdx))
                    {
                        foreach (var eidx in elemIdx)
                        {
                            elemIdx_FireWorkIdx[eidx, fire] = true;
                        }
                    }
                }
                progress.Report(((int)Math.Round((double)fire / igntionWorks.Count * 100), $"{fire + 1} Ignition work processes, total:{igntionWorks.Count}"));
            }
            //crereaete combo
            int numElems = elemIdx_FireWorkIdx.GetUpperBound(0) + 1;
            for (int elemId = 0; elemId <= numElems - 1; elemId++)
            {
                var elem = elemTemp[elemId];
                bool elemUnderFire = false;
                for (int fire = 0; fire <= elemIdx_FireWorkIdx.GetUpperBound(1); fire++)
                {
                    if (elemIdx_FireWorkIdx[elemId, fire] == true)
                    {
                        var workFire = igntionWorks[fire];
                        if (!elem.IsElementVoidDuringWork(workFire))
                        {
                            elemUnderFire = true;
                            var comb = new HazardCombination(elem, workFire);
                            elem.Combinations.Add(comb);
                        }
                    }
                }
                if (elemUnderFire == false)
                {
                    var comb = new HazardCombination(elem);
                    elem.Combinations.Add(comb);
                }
                int percentage = (int)Math.Round((double)(elemId + 1) / numElems * 100);
                if (percentage % 10 == 0)
                    progress.Report((percentage, $"{elemId + 1} elements scanned, total:{numElems}"));
            }
        }

        /// <summary>
        /// Generate Chunks for a given work
        /// </summary>
        /// <param name="currentWork">Current work</param>
        /// <param name="defaultFireRange">Fire protection range</param>
        /// <param name="timeBuffer">time buffer considering the remaining of flamable gas</param>
        /// <param name="elemId_InternalId">elem-mat Id</param>
        /// <returns>chunks </returns>
        public void IdentifyGlobalFireHazard_VoxelBox2_ConsideringHeight(double defaultFireRange, Dictionary<string, string> elemId_InternalId, IProgress<(int, string)> progress)
        {
            //update elem index
            int i = 0;
            foreach (var elem in this.ElemBoxRel.Values)
            {
                elem.Combinations = new List<HazardCombination>();
                elem.UpdateIndex(i);
                i += 1;
            }
            var elemTemp = this.ElemBoxRel.Values.ToList();

            //expand voxels 2D
            Dictionary<CellIndex, List<int>> cix_ElemIdx = new Dictionary<CellIndex, List<int>>();

            //get combustible elements
            List<Work> igntionWorks = new List<Work>();
            foreach (var work in this.Works)
            {
                if (work.EmitSparks)
                {
                    igntionWorks.Add(work);
                }
            }
            bool[,] elemIdx_FireWorkIdx = new bool[this.ElemBoxRel.Values.Count, igntionWorks.Count];
            for (int fire = 0; fire <= igntionWorks.Count - 1; fire++)
            {
                var workFire = igntionWorks[fire];
                List<HazardBoxElementInfo> ignitionElems = new List<HazardBoxElementInfo>();
                foreach (var elemId in workFire.ElementIds)
                {
                    if (this.ElemBoxRel.TryGetValue(elemId, out var elem))
                    {
                        ignitionElems.Add(elem);
                    }
                }
                //get fire separation range
                var ignitionRange2D = this.GetFireSeparationRange2(ignitionElems, defaultFireRange);
                //separation fire
                foreach (var cixFire in ignitionRange2D)
                {
                    if (cix_ElemIdx.TryGetValue(cixFire, out var elemIdx))
                    {
                        foreach (var eidx in elemIdx)
                        {
                            elemIdx_FireWorkIdx[eidx, fire] = true;
                        }
                    }
                }
                progress.Report(((int)Math.Round((double)fire / igntionWorks.Count * 100), $"{fire + 1} Ignition work processes, total:{igntionWorks.Count}"));
            }
            //crereaete combo
            int numElems = elemIdx_FireWorkIdx.GetUpperBound(0) + 1;
            for (int elemId = 0; elemId <= numElems - 1; elemId++)
            {
                var elem = elemTemp[elemId];
                bool elemUnderFire = false;
                for (int fire = 0; fire <= elemIdx_FireWorkIdx.GetUpperBound(1); fire++)
                {
                    if (elemIdx_FireWorkIdx[elemId, fire] == true)
                    {
                        var workFire = igntionWorks[fire];
                        if (!elem.IsElementVoidDuringWork(workFire))
                        {
                            elemUnderFire = true;
                            var comb = new HazardCombination(elem, workFire);
                            elem.Combinations.Add(comb);
                        }
                    }
                }
                if (elemUnderFire == false)
                {
                    var comb = new HazardCombination(elem);
                    elem.Combinations.Add(comb);
                }
                int percentage = (int)Math.Round((double)(elemId + 1) / numElems * 100);
                if (percentage % 10 == 0)
                    progress.Report((percentage, $"{elemId + 1} elements scanned, total:{numElems}"));
            }
        }
       



        /// <summary>
        /// Generate Chunks for a given work
        /// </summary>
        /// <param name="currentWork">Current work</param>
        /// <param name="defaultFireRange">Fire protection range</param>
        /// <param name="timeBuffer">time buffer considering the remaining of flamable gas</param>
        /// <param name="elemId_InternalId">elem-mat Id</param>
        /// <returns>chunks </returns>
        public void IdentifyGlobalFireHazard_Mesh(double defaultFireRange, Dictionary<string, string> elemId_InternalId, IProgress<(int, string)> progress)
        {
            //update elem index
            int i = 0;
            foreach (var elem in this.ElemMeshRel.Values)
            {
                elem.Combinations = new List<HazardCombination>();
                elem.UpdateIndex(i);
                i += 1;
            }
            var elemTemp = this.ElemMeshRel.Values.ToList();
            var result = new List<HazardCombination>();

            //get combustible elements
            List<Work> igntionWorks = new List<Work>();
            foreach (var work in this.Works)
            {
                if (work.EmitSparks)
                {
                    igntionWorks.Add(work);
                }
            }

            bool[] elemIdxUnderFire = new bool[elemTemp.Count];
            //crereaete combo

            for (int fire = 0; fire <= igntionWorks.Count - 1; fire++)
            {
                var workFire = igntionWorks[fire];
                DateTime fireSt = workFire.Get_Start();
                DateTime fireEd = workFire.Get_Finish(true);
                List<HazardMeshElementInfo> ignitionElems = new List<HazardMeshElementInfo>();
                foreach (var elemId in workFire.ElementIds)
                {
                    if (this.ElemMeshRel.TryGetValue(elemId, out var elem))
                    {
                        ignitionElems.Add(elem);
                    }
                }
                //find active element
                List<HazardMeshElementInfo> activeElem = new List<HazardMeshElementInfo>();
                foreach (var elem in elemTemp)
                {
                    if (!elem.IsElementVoidDuringWork(workFire))
                    {
                        activeElem.Add(elem);
                    }
                }
                //try ignite elements
                List<HazardMeshElementInfo> elemOnFire = new List<HazardMeshElementInfo>();
                foreach (var elem_dis in SpatialSearchTool.GetElementsWithinDistance(ignitionElems, activeElem, defaultFireRange))
                {
                    var elem = elem_dis.elem;
                    var dis = elem_dis.distance;
                    elem.DistanceToFire = dis;
                    elemIdxUnderFire[elem.GetIndex()] = true;
                    //check elem linking to current element
                    var combo = new HazardCombination(elem, workFire) { Distance = dis };
                    elem.Combinations.Add(combo);
                }
                //tempCheck
                progress.Report(((int)Math.Round((double)fire / igntionWorks.Count * 100), $"{fire} Ignition work processes, total:{igntionWorks.Count}"));
            }
            //add elem not on fire
            for (int j = 0; j < elemIdxUnderFire.Length; j++)
            {
                if (elemIdxUnderFire[j] == false)
                {
                    var elemSafe = elemTemp[j];
                    elemSafe.Combinations.Add(new HazardCombination(elemSafe));
                }
            }
        }




        private List<(int col, int row)> GetFireSeparationRange(List<HazardVoxelElementInfo> igniteElems, double protectionRange)
        {
            //get fire spread range
            var fireRange = new FireSearchRange2D(protectionRange, this.VoxelSize);
            var searchOffset = (int)Math.Ceiling(Math.Round(protectionRange / this.VoxelSize, 3));
            //use an array to store the union of the fire range
            HashSet<CellIndex> cix2Scan = new HashSet<CellIndex>();
            var cix_FireElem = GetCellIndexRangeOfElement(igniteElems, out var colFireMin, out var rowFireMin);
            var colFireMax = cix_FireElem.GetUpperBound(0);
            var rowFireMax = cix_FireElem.GetUpperBound(1);
            //check the overlap of fire and vox
            for (int col = 0; col <= colFireMax; col++)
            {
                for (int row = 0; row <= rowFireMax; row++)
                {
                    //if there is no valid ignition voxels then skip
                    if (cix_FireElem[col, row] == null)
                    {
                        continue;
                    }
                    //scan potential voxels above the ignition sources
                    var colGlobal = col + colFireMin;
                    var rowGlobal = row + rowFireMin;
                    cix2Scan.Add(new CellIndex(colGlobal, rowGlobal));

                    //check the surrounding of the cellOffset
                    int[] colOffsetNear = new int[4] { 1, 0, -1, 0 };
                    int[] rowOffsetNear = new int[4] { 0, 1, 0, -1 };
                    bool[] edgeOutside = new bool[4];
                    bool onBoundary = false;
                    for (int i = 0; i <= 3; i++)
                    {
                        var colNear = col + colOffsetNear[i];
                        var rowNear = row + rowOffsetNear[i];
                        if (!TryGetCellIndexes(cix_FireElem, colNear, rowNear, out var elemFound))//current vox is a boundary one
                        {
                            edgeOutside[i] = true;
                            onBoundary = true;
                        }
                    }
                    //skip cells not on the boundary
                    if (!onBoundary)
                    {
                        continue;
                    }
                    for (int i = 0; i <= 3; i++)
                    {
                        var curEdgeIdx = (i % 4);
                        var nextEdgeIdx = ((i + 1) % 4);
                        if (edgeOutside[curEdgeIdx] == true)// current edge is outside
                        {
                            var dir0 = (RangeDirection)(curEdgeIdx * 2);
                            foreach (var cix in fireRange.GetCellsOfDirection(dir0))
                            {
                                var colFireGlobal = col + cix.Col + colFireMin;
                                var rowFireGlobal = row + cix.Row + rowFireMin;
                                cix2Scan.Add(new CellIndex(colFireGlobal, rowFireGlobal));

                            }
                            if (edgeOutside[nextEdgeIdx] == true)//one point is outside, scan a fan area
                            {
                                var dir1 = (RangeDirection)(nextEdgeIdx * 2);
                                foreach (var cix in fireRange.GetCellBetween(dir0, dir1, false, false))
                                {
                                    var colFireGlobal = col + cix.Col + colFireMin;
                                    var rowFireGlobal = row + cix.Row + rowFireMin;
                                    cix2Scan.Add(new CellIndex(colFireGlobal, rowFireGlobal));

                                }
                            }
                        }
                    }
                }
            }
            //get elements not covered by fires
            List<(int col, int row)> result = new List<(int col, int row)>();
            foreach (var item in cix2Scan)
            {
                result.Add((item.Col, item.Row));
            }
            return result;
        }

        private HashSet<CellIndex> GetFireSeparationRange2(List<HazardVoxelElementInfo> igniteElems, double protectionRange)
        {
            //get fire spread range
            var fireRange = new FireSearchRange2D(protectionRange, this.VoxelSize);
            var searchOffset = (int)Math.Ceiling(Math.Round(protectionRange / this.VoxelSize, 3));
            //use an array to store the union of the fire range
            HashSet<CellIndex> cix2Scan = new HashSet<CellIndex>();
            var cix_FireElem = GetCellIndexRangeOfElement(igniteElems, out var colFireMin, out var rowFireMin);
            var colFireMax = cix_FireElem.GetUpperBound(0);
            var rowFireMax = cix_FireElem.GetUpperBound(1);
            //check the overlap of fire and vox
            for (int col = 0; col <= colFireMax; col++)
            {
                for (int row = 0; row <= rowFireMax; row++)
                {
                    //if there is no valid ignition voxels then skip
                    if (cix_FireElem[col, row] == null)
                    {
                        continue;
                    }
                    //scan potential voxels above the ignition sources
                    var colGlobal = col + colFireMin;
                    var rowGlobal = row + rowFireMin;
                    cix2Scan.Add(new CellIndex(colGlobal, rowGlobal));

                    //check the surrounding of the cellOffset
                    int[] colOffsetNear = new int[4] { 1, 0, -1, 0 };
                    int[] rowOffsetNear = new int[4] { 0, 1, 0, -1 };
                    bool[] edgeOutside = new bool[4];
                    bool onBoundary = false;
                    for (int i = 0; i <= 3; i++)
                    {
                        var colNear = col + colOffsetNear[i];
                        var rowNear = row + rowOffsetNear[i];
                        if (!TryGetCellIndexes(cix_FireElem, colNear, rowNear, out var elemFound))//current vox is a boundary one
                        {
                            edgeOutside[i] = true;
                            onBoundary = true;
                        }
                    }
                    //skip cells not on the boundary
                    if (!onBoundary)
                    {
                        continue;
                    }
                    for (int i = 0; i <= 3; i++)
                    {
                        var curEdgeIdx = (i % 4);
                        var nextEdgeIdx = ((i + 1) % 4);
                        if (edgeOutside[curEdgeIdx] == true)// current edge is outside
                        {
                            var dir0 = (RangeDirection)(curEdgeIdx * 2);
                            foreach (var cix in fireRange.GetCellsOfDirection(dir0))
                            {
                                var colFireGlobal = col + cix.Col + colFireMin;
                                var rowFireGlobal = row + cix.Row + rowFireMin;
                                cix2Scan.Add(new CellIndex(colFireGlobal, rowFireGlobal));

                            }
                            if (edgeOutside[nextEdgeIdx] == true)//one point is outside, scan a fan area
                            {
                                var dir1 = (RangeDirection)(nextEdgeIdx * 2);
                                foreach (var cix in fireRange.GetCellBetween(dir0, dir1, false, false))
                                {
                                    var colFireGlobal = col + cix.Col + colFireMin;
                                    var rowFireGlobal = row + cix.Row + rowFireMin;
                                    cix2Scan.Add(new CellIndex(colFireGlobal, rowFireGlobal));

                                }
                            }
                        }
                    }
                }
            }

            return cix2Scan;
        }
        private HashSet<CellIndex> GetFireSeparationRange2(List<HazardBoxElementInfo> igniteElems, double protectionRange)
        {
            //get fire spread range
            var fireRange = new FireSearchRange2D(protectionRange, this.VoxelSize);
            var searchOffset = (int)Math.Ceiling(Math.Round(protectionRange / this.VoxelSize, 3));
            //use an array to store the union of the fire range
            HashSet<CellIndex> cix2Scan = new HashSet<CellIndex>();
            var cix_FireElem = GetCellIndexRangeOfElement(igniteElems, out var colFireMin, out var rowFireMin);
            var colFireMax = cix_FireElem.GetUpperBound(0);
            var rowFireMax = cix_FireElem.GetUpperBound(1);
            //check the overlap of fire and vox
            for (int col = 0; col <= colFireMax; col++)
            {
                for (int row = 0; row <= rowFireMax; row++)
                {
                    //if there is no valid ignition voxels then skip
                    if (cix_FireElem[col, row] == null)
                    {
                        continue;
                    }
                    //scan potential voxels above the ignition sources
                    var colGlobal = col + colFireMin;
                    var rowGlobal = row + rowFireMin;
                    cix2Scan.Add(new CellIndex(colGlobal, rowGlobal));

                    //check the surrounding of the cellOffset
                    int[] colOffsetNear = new int[4] { 1, 0, -1, 0 };
                    int[] rowOffsetNear = new int[4] { 0, 1, 0, -1 };
                    bool[] edgeOutside = new bool[4];
                    bool onBoundary = false;
                    for (int i = 0; i <= 3; i++)
                    {
                        var colNear = col + colOffsetNear[i];
                        var rowNear = row + rowOffsetNear[i];
                        if (!TryGetCellIndexes(cix_FireElem, colNear, rowNear, out var elemFound))//current vox is a boundary one
                        {
                            edgeOutside[i] = true;
                            onBoundary = true;
                        }
                    }
                    //skip cells not on the boundary
                    if (!onBoundary)
                    {
                        continue;
                    }
                    for (int i = 0; i <= 3; i++)
                    {
                        var curEdgeIdx = (i % 4);
                        var nextEdgeIdx = ((i + 1) % 4);
                        if (edgeOutside[curEdgeIdx] == true)// current edge is outside
                        {
                            var dir0 = (RangeDirection)(curEdgeIdx * 2);
                            foreach (var cix in fireRange.GetCellsOfDirection(dir0))
                            {
                                var colFireGlobal = col + cix.Col + colFireMin;
                                var rowFireGlobal = row + cix.Row + rowFireMin;
                                cix2Scan.Add(new CellIndex(colFireGlobal, rowFireGlobal));

                            }
                            if (edgeOutside[nextEdgeIdx] == true)//one point is outside, scan a fan area
                            {
                                var dir1 = (RangeDirection)(nextEdgeIdx * 2);
                                foreach (var cix in fireRange.GetCellBetween(dir0, dir1, false, false))
                                {
                                    var colFireGlobal = col + cix.Col + colFireMin;
                                    var rowFireGlobal = row + cix.Row + rowFireMin;
                                    cix2Scan.Add(new CellIndex(colFireGlobal, rowFireGlobal));

                                }
                            }
                        }
                    }
                }
            }

            return cix2Scan;
        }
        /// <summary>
        /// Test: if the fire has vertical range
        /// </summary>
        /// <param name="igniteElems">Ignitnon elements</param>
        /// <param name="protectionRange"></param>
        /// <returns></returns>
        private HashSet<CellIndex> GetFireSeparationRange2_ConsiderVerticalRange(List<HazardBoxElementInfo> igniteElems, double protectionRange)
        {
            //get fire spread range
            var fireRange = new FireSearchRange2D(protectionRange, this.VoxelSize);
            var searchOffset = (int)Math.Ceiling(Math.Round(protectionRange / this.VoxelSize, 3));
            //use an array to store the union of the fire range
            HashSet<CellIndex> cix2Scan = new HashSet<CellIndex>();
            var cix_FireElem = GetCellIndexRangeOfElement(igniteElems, out var colFireMin, out var rowFireMin);
            var colFireMax = cix_FireElem.GetUpperBound(0);
            var rowFireMax = cix_FireElem.GetUpperBound(1);
            //check the overlap of fire and vox
            for (int col = 0; col <= colFireMax; col++)
            {
                for (int row = 0; row <= rowFireMax; row++)
                {
                    //if there is no valid ignition voxels then skip
                    if (cix_FireElem[col, row] == null)
                    {
                        continue;
                    }
                    //scan potential voxels above the ignition sources
                    var colGlobal = col + colFireMin;
                    var rowGlobal = row + rowFireMin;
                    cix2Scan.Add(new CellIndex(colGlobal, rowGlobal));

                    //check the surrounding of the cellOffset
                    int[] colOffsetNear = new int[4] { 1, 0, -1, 0 };
                    int[] rowOffsetNear = new int[4] { 0, 1, 0, -1 };
                    bool[] edgeOutside = new bool[4];
                    bool onBoundary = false;
                    for (int i = 0; i <= 3; i++)
                    {
                        var colNear = col + colOffsetNear[i];
                        var rowNear = row + rowOffsetNear[i];
                        if (!TryGetCellIndexes(cix_FireElem, colNear, rowNear, out var elemFound))//current vox is a boundary one
                        {
                            edgeOutside[i] = true;
                            onBoundary = true;
                        }
                    }
                    //skip cells not on the boundary
                    if (!onBoundary)
                    {
                        continue;
                    }
                    for (int i = 0; i <= 3; i++)
                    {
                        var curEdgeIdx = (i % 4);
                        var nextEdgeIdx = ((i + 1) % 4);
                        if (edgeOutside[curEdgeIdx] == true)// current edge is outside
                        {
                            var dir0 = (RangeDirection)(curEdgeIdx * 2);
                            foreach (var cix in fireRange.GetCellsOfDirection(dir0))
                            {
                                var colFireGlobal = col + cix.Col + colFireMin;
                                var rowFireGlobal = row + cix.Row + rowFireMin;
                                cix2Scan.Add(new CellIndex(colFireGlobal, rowFireGlobal));

                            }
                            if (edgeOutside[nextEdgeIdx] == true)//one point is outside, scan a fan area
                            {
                                var dir1 = (RangeDirection)(nextEdgeIdx * 2);
                                foreach (var cix in fireRange.GetCellBetween(dir0, dir1, false, false))
                                {
                                    var colFireGlobal = col + cix.Col + colFireMin;
                                    var rowFireGlobal = row + cix.Row + rowFireMin;
                                    cix2Scan.Add(new CellIndex(colFireGlobal, rowFireGlobal));

                                }
                            }
                        }
                    }
                }
            }

            return cix2Scan;
        }

        private List<HazardVoxelElementInfo> SpreadFire2(List<(int col, int row)> fireRange2D, List<HazardVoxelElementInfo> validElemInfos, out List<HazardVoxelElementInfo> elemNotAffected)
        {
            List<HazardVoxelElementInfo> result = new List<HazardVoxelElementInfo>() { Capacity = validElemInfos.Count };
            HashSet<CellIndex> cix2Scan = new HashSet<CellIndex>();
            //reset index of combustible elems
            for (int i = 0; i <= validElemInfos.Count - 1; i++)
            {
                var elem = validElemInfos[i];
                elem.UpdateIndex(i);
            }
            //mark if an element has been checked
            bool[] elemChecked = new bool[validElemInfos.Count];

            //use an array to store the union of the fire range
            //scan potential voxels above the ignition sources
            var cix_ElemOnFire = GetCellIndexRangeOfElement(validElemInfos, out var colMin, out var rowMin);
            //check the overlap of fire and vox
            bool[,] fireOverlap = new bool[cix_ElemOnFire.GetUpperBound(0) + 1, cix_ElemOnFire.GetUpperBound(1) + 1];
            foreach (var fireIdx2D in fireRange2D)
            {
                var colGlobal = fireIdx2D.col;
                var rowGlobal = fireIdx2D.row;

                var colInAll = colGlobal - colMin;
                var rowInAll = rowGlobal - rowMin;
                if (TryGetCellIndexes(cix_ElemOnFire, colInAll, rowInAll, out var elemFound))
                {
                    if (fireOverlap[colInAll, rowInAll] == false)// the vulunable elements is not on fire yet,
                    {
                        fireOverlap[colInAll, rowInAll] = true;
                        //modify elem check status
                        foreach (var elem in elemFound)
                        {
                            var eidx = elem.GetIndex();
                            if (elemChecked[eidx] == false)
                            {
                                elemChecked[eidx] = true;

                                result.Add(elem);
                            }
                        }
                    }
                }
            }

            //get elements not covered by fires
            elemNotAffected = new List<HazardVoxelElementInfo>();
            for (int i = 0; i <= validElemInfos.Count - 1; i++)
            {
                if (elemChecked[i] == false)
                {
                    elemNotAffected.Add(validElemInfos[i]);
                }
            }
            return result;
        }

        /// <summary>
        /// try get the the elements with the col and row index of col and row
        /// </summary>
        /// <param name="elemArray">input elements</param>
        /// <param name="col">col index</param>
        /// <param name="row">row indxe</param>
        /// <param name="elemFound">eelement if exists</param>
        /// <returns>true if element is found, false otherwise</returns>
        private bool TryGetCellIndexes(List<HazardVoxelElementInfo>[,] elemArray, int col, int row, out List<HazardVoxelElementInfo> elemFound)
        {
            var arrayMaxCol = elemArray.GetUpperBound(0);
            var arrayMaxRow = elemArray.GetUpperBound(1);
            if (col >= 0 && col <= arrayMaxCol && row >= 0 && row <= arrayMaxRow)
            {
                elemFound = elemArray[col, row];
                if (elemFound != null && elemFound.Count != 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                elemFound = null;
                return false;
            }
        }

        private bool TryGetCellIndexes(List<HazardBoxElementInfo>[,] elemArray, int col, int row, out List<HazardBoxElementInfo> elemFound)
        {
            var arrayMaxCol = elemArray.GetUpperBound(0);
            var arrayMaxRow = elemArray.GetUpperBound(1);
            if (col >= 0 && col <= arrayMaxCol && row >= 0 && row <= arrayMaxRow)
            {
                elemFound = elemArray[col, row];
                if (elemFound != null && elemFound.Count != 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                elemFound = null;
                return false;
            }
        }

        /// <summary>
        /// Get voxels based on input cell indexees
        /// </summary>
        /// <param name="voxArray"></param>
        /// <param name="col"></param>
        /// <param name="row"></param>
        /// <param name="voxFound"></param>
        /// <returns></returns>
        private bool TryGetVoxelsFromCellIndexes(List<HazardVoxel>[,] voxArray, int col, int row, out List<HazardVoxel> voxFound)
        {
            var arrayMaxCol = voxArray.GetUpperBound(0);
            var arrayMaxRow = voxArray.GetUpperBound(1);
            if (col >= 0 && col <= arrayMaxCol && row >= 0 && row <= arrayMaxRow)
            {
                voxFound = voxArray[col, row];
                if (voxFound != null && voxFound.Count != 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                voxFound = null;
                return false;
            }
        }
        private List<HazardVoxelElementInfo>[,] GetCellIndexRangeOfElement(ICollection<HazardVoxelElementInfo> elements, out int colMin, out int rowMin)
        {
            //get the range of cix_Vox
            colMin = int.MaxValue;
            var colMax = int.MinValue;
            rowMin = int.MaxValue;
            var rowMax = int.MinValue;
            foreach (var elem in elements)
            {
                foreach (var vox in elem.Voxels)
                {
                    var col = vox.ColIndex;
                    var row = vox.RowIndex;
                    colMin = Math.Min(colMin, col);
                    rowMin = Math.Min(rowMin, row);
                    colMax = Math.Max(colMax, col);
                    rowMax = Math.Max(rowMax, row);
                }
            }
            if (colMin > colMax)//no update happens
            {
                colMin = 0;
                colMax = 0;
                rowMin = 0;
                rowMax = 0;
                return new List<HazardVoxelElementInfo>[0, 0];
            }
            else
            {
                var colCount = colMax - colMin + 1;
                var rowCount = rowMax - rowMin + 1;
                var result = new List<HazardVoxelElementInfo>[colCount, rowCount];
                foreach (var elem in elements)
                {
                    foreach (var vox in elem.Voxels)
                    {
                        var col = vox.ColIndex;
                        var row = vox.RowIndex;
                        var colLoc = col - colMin;
                        var rowLoc = row - rowMin;
                        if (result[colLoc, rowLoc] == null)
                        {
                            result[colLoc, rowLoc] = new List<HazardVoxelElementInfo>();
                        }
                        result[colLoc, rowLoc].Add(elem);
                    }
                }
                return result;
            }
        }

        private List<HazardBoxElementInfo>[,] GetCellIndexRangeOfElement(ICollection<HazardBoxElementInfo> elements, out int colMin, out int rowMin)
        {
            //get the range of cix_Vox
            colMin = int.MaxValue;
            var colMax = int.MinValue;
            rowMin = int.MaxValue;
            var rowMax = int.MinValue;
            foreach (var elem in elements)
            {
                foreach (var cix in elem.Get2DProjection((int)this.VoxelSize))
                {
                    var col = cix.Col;
                    var row = cix.Row;
                    colMin = Math.Min(colMin, col);
                    rowMin = Math.Min(rowMin, row);
                    colMax = Math.Max(colMax, col);
                    rowMax = Math.Max(rowMax, row);
                }
            }
            if (colMin > colMax)//no update happens
            {
                colMin = 0;
                colMax = 0;
                rowMin = 0;
                rowMax = 0;
                return new List<HazardBoxElementInfo>[0, 0];
            }
            else
            {
                var colCount = colMax - colMin + 1;
                var rowCount = rowMax - rowMin + 1;
                var result = new List<HazardBoxElementInfo>[colCount, rowCount];
                foreach (var elem in elements)
                {
                    foreach (var cix in elem.Get2DProjection((int)this.VoxelSize))
                    {
                        var col = cix.Col;
                        var row = cix.Row;
                        var colLoc = col - colMin;
                        var rowLoc = row - rowMin;
                        if (result[colLoc, rowLoc] == null)
                        {
                            result[colLoc, rowLoc] = new List<HazardBoxElementInfo>();
                        }
                        result[colLoc, rowLoc].Add(elem);
                    }
                }
                return result;
            }
        }
        /// <summary>
        /// Get Cell Index Range of Element, consider vertical range
        /// </summary>
        /// <param name="elements"></param>
        /// <param name="btmElev"></param>
        /// <param name="topElev"></param>
        /// <param name="colMin"></param>
        /// <param name="rowMin"></param>
        /// <returns></returns>
        private List<HazardBoxElementInfo>[,] GetCellIndexRangeOfElement(ICollection<HazardBoxElementInfo> elements, int btmElev, int topElev, out int colMin, out int rowMin)
        {
            //get the range of cix_Vox
            colMin = int.MaxValue;
            var colMax = int.MinValue;
            rowMin = int.MaxValue;
            var rowMax = int.MinValue;
            foreach (var elem in elements)
            {
                foreach (var cix in elem.Get2DProjectionIntersecing((int)this.VoxelSize, btmElev, topElev))
                {
                    var col = cix.Col;
                    var row = cix.Row;
                    colMin = Math.Min(colMin, col);
                    rowMin = Math.Min(rowMin, row);
                    colMax = Math.Max(colMax, col);
                    rowMax = Math.Max(rowMax, row);
                }
            }
            if (colMin > colMax)//no update happens
            {
                colMin = 0;
                colMax = 0;
                rowMin = 0;
                rowMax = 0;
                return new List<HazardBoxElementInfo>[0, 0];
            }
            else
            {
                var colCount = colMax - colMin + 1;
                var rowCount = rowMax - rowMin + 1;
                var result = new List<HazardBoxElementInfo>[colCount, rowCount];
                foreach (var elem in elements)
                {
                    foreach (var cix in elem.Get2DProjection((int)this.VoxelSize))
                    {
                        var col = cix.Col;
                        var row = cix.Row;
                        var colLoc = col - colMin;
                        var rowLoc = row - rowMin;
                        if (result[colLoc, rowLoc] == null)
                        {
                            result[colLoc, rowLoc] = new List<HazardBoxElementInfo>();
                        }
                        result[colLoc, rowLoc].Add(elem);
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// place voxels in arrays
        /// </summary>
        /// <param name="elements"></param>
        /// <param name="colMin"></param>
        /// <param name="rowMin"></param>
        /// <returns></returns>

        private List<HazardVoxel>[,] GetCellIndexRangeOfVoxels(ICollection<HazardVoxelElementInfo> elements, out int colMin, out int rowMin)
        {
            //get the range of cix_Vox
            colMin = int.MaxValue;
            var colMax = int.MinValue;
            rowMin = int.MaxValue;
            var rowMax = int.MinValue;
            foreach (var elem in elements)
            {
                foreach (var vox in elem.Voxels)
                {
                    var col = vox.ColIndex;
                    var row = vox.RowIndex;
                    colMin = Math.Min(colMin, col);
                    rowMin = Math.Min(rowMin, row);
                    colMax = Math.Max(colMax, col);
                    rowMax = Math.Max(rowMax, row);
                }
            }
            if (colMin > colMax)//no update happens
            {
                colMin = 0;
                colMax = 0;
                rowMin = 0;
                rowMax = 0;
                return new List<HazardVoxel>[0, 0];
            }
            else
            {
                var colCount = colMax - colMin + 1;
                var rowCount = rowMax - rowMin + 1;
                var result = new List<HazardVoxel>[colCount, rowCount];
                foreach (var elem in elements)
                {
                    foreach (var vox in elem.Voxels)
                    {
                        var col = vox.ColIndex;
                        var row = vox.RowIndex;
                        var colLoc = col - colMin;
                        var rowLoc = row - rowMin;
                        if (result[colLoc, rowLoc] == null)
                        {
                            result[colLoc, rowLoc] = new List<HazardVoxel>();
                        }
                        result[colLoc, rowLoc].Add(vox);
                    }
                }
                return result;
            }
        }

        

    }

    public class HazardVoxel
    {
        public int ColIndex { get; set; }
        public int RowIndex { get; set; }
        public double BottomElevation { get; set; }
        public double TopElevation { get; set; }

    }



    public class RiskChunk
    {
        public DateTime StartTime { get; set; } = DateTime.Now;
        public DateTime EndTime { get; set; } = DateTime.Now;
        public Work Work { get; set; }
        public List<HazardVoxel> Results;
        public RiskIdentifier RiskIdentifier { get; set; }
        /// <summary>
        /// a string:"{elementId},{affWorkIds}"
        /// </summary>
        public string Info { get; set; }

        public RiskChunk(RiskIdentifier riskIdentifier, Work work, List<HazardVoxel> voxels)
        {
            this.RiskIdentifier = riskIdentifier;
            this.StartTime = work.Get_Start();
            this.EndTime = work.Get_Finish(true);
            this.Results = voxels;
            this.Work = work;
        }



        public string DescribeRisk2(List<Document> docs)
        {
            var chunkData = this.Info.Split('_');
            //find name of the voxel
            var elemId = chunkData[0];
            var docIdx = int.Parse(elemId.Split('$')[0]);
            var elementId = new ElementId(int.Parse(elemId.Split('$')[1]));
            var curDoc = docs[docIdx];
            var elem = docs[docIdx].GetElement(elementId);
            string strElem = elem.Name;
            //get eleemet description
            List<string> elemInfo = new List<string>();
            foreach (var matId in elem.GetMaterialIds(false).Union(elem.GetMaterialIds(true)))
            {
                var mat = curDoc.GetElement(matId) as Material;
                if (mat != null)
                {
                    elemInfo.Add(mat.Name);
                }
            }
            if (elemInfo.Count == 0)
            {
                elemInfo.Add("暂无");
            }

            //find work affected by other
            List<string> workAffect = new List<string>();
            int workNumber = 0;
            for (int i = 1; i <= chunkData.Length - 1; i++)
            {
                var kvp = chunkData[i];
                var work = this.RiskIdentifier.WorkMap[kvp];
                workAffect.Add(workNumber.ToString() + "." + work.GetWorkDescriptionIncludeTime());
                workNumber += 1;
            }
            if (workAffect.Count == 0)
            {
                workAffect.Add("暂无");
            }
            //Generate description
            string strResult = $"当前时间段为:{this.StartTime.ToShortDateString()}-{this.EndTime.ToShortDateString()}\r\n,当前区域的构件为：{strElem},\r\n" +
                $"材质是：{string.Join("; ", elemInfo)},\r\n" +
                $"该区域已经完成，正在进行或将要进行的工作:\r\n{string.Join("\r\n", workAffect)}\r\n";
            return strResult;
        }
        /// <summary>
        /// Get the element Ids of the work relating to the chunk
        /// </summary>
        /// <param name="otherElementId">the element ids of other works intersecting with current vorks</param>
        /// <returns>the element ids of the work</returns>
        public HashSet<string> GetBaseElementIds(out HashSet<string> otherElementId)
        {
            string[] strInfo = this.Info.Split('_');
            HashSet<string> result = new HashSet<string>();
            otherElementId = new HashSet<string>();
            foreach (var id in strInfo)
            {
                if (this.RiskIdentifier.WorkMap.ContainsKey(id))
                {
                    var work = this.RiskIdentifier.WorkMap[id];
                    if (id == this.Work.Id)
                    {
                        foreach (var eid in work.ElementIds)
                        {
                            result.Add(eid);
                        }
                    }
                    else
                    {
                        foreach (var eid in work.ElementIds)
                        {
                            otherElementId.Add(eid);
                        }
                    }
                }
            }
            return result;
        }





    }
    public class HazardVoxelInfo
    {
        public int Col { get; set; }
        public int Row { get; set; }
        public double Elevation { get; set; }
        public bool IsBoundaryVoxel { get; set; }
        public bool AffectedByFFH { get; set; }

        public List<string> WorkIds { get; set; }
        public string ElementId { get; set; }
        public List<string> AffectWorkIds { get; internal set; }




    }

    public class _4DElement
    {
        public string ElementId { get; set; }

        public List<Work> Works { get; set; }
        public List<HazardMaterial> Materials { get; set; } = new List<HazardMaterial>();
        protected int tempIndex = 0;
        public List<Tuple<DateTime, DateTime, ElementStatus>> ElemStausPeroid { get; set; } = new List<Tuple<DateTime, DateTime, ElementStatus>>();

        public List<HazardCombination> Combinations { get; set; } = new List<HazardCombination>();

        public void SetElementTimeData(List<Work> works)
        {
            this.Works = works.OrderBy(x => x.Get_Start()).ToList();
            Work firstValidWork = this.Works.Where(c => c.WorkType != WorkType.Other).FirstOrDefault();
            //create 2 dummy work
            Work dummySt = new Work() { Id = "-1" };
            dummySt.WorkType = (firstValidWork.WorkType == WorkType.Construct ? WorkType.Demolish : WorkType.Construct);
            dummySt.Set_Start(DateTime.MinValue);
            dummySt.Set_Finish(DateTime.MinValue);
            Work dummyEd = new Work() { Id = "-1" };
            dummyEd.Set_Start(DateTime.MaxValue);
            dummyEd.Set_Finish(DateTime.MaxValue);
            List<Work> expendedWork = new List<Work>() { Capacity = this.Works.Count + 2 };
            expendedWork.Add(dummySt);
            expendedWork.AddRange(this.Works);
            expendedWork.Add(dummyEd);

            //find first work
            int snakePointer = 0;
            int foodPointer = snakePointer + 1;
            var snakeWork = expendedWork[snakePointer];
            var snakeStatus = snakeWork.WorkType;
            DateTime peroidSt = snakeWork.Get_Start();
            DateTime peroidEd = snakeWork.Get_Finish(true);
            var snakeEndType = snakeWork.WorkType;
            while (foodPointer < expendedWork.Count)
            {
                var foodWork = expendedWork[foodPointer];
                if (snakeWork.Get_Finish(true) >= foodWork.Get_Start())//2 element intersects
                {
                    if (foodWork.Get_Finish(true) > peroidEd)
                    {
                        peroidEd = foodWork.Get_Finish(true);
                        if (foodWork.WorkType != WorkType.Other)
                        {
                            snakeEndType = foodWork.WorkType;
                        }
                    }
                    foodPointer += 1;
                }
                else// the element is not intersects
                {
                    if (snakePointer != 0)
                    {
                        ElemStausPeroid.Add(new Tuple<DateTime, DateTime, ElementStatus>(peroidSt, peroidEd, ElementStatus.Active));
                    }
                    if (snakeEndType == WorkType.Demolish)//the element after this peroid is void
                    {
                        ElemStausPeroid.Add(new Tuple<DateTime, DateTime, ElementStatus>(snakeWork.Get_Finish(true), foodWork.Get_Start(), ElementStatus.Void));
                    }
                    else if (snakeEndType == WorkType.Construct)//the peroid afterer the work is quiescent
                    {
                        ElemStausPeroid.Add(new Tuple<DateTime, DateTime, ElementStatus>(snakeWork.Get_Finish(true), foodWork.Get_Start(), ElementStatus.Quiescent));
                    }
                    //update snake work
                    snakePointer = foodPointer;
                    snakeWork = expendedWork[snakePointer];
                    if (snakeWork.WorkType != WorkType.Other)
                    {
                        snakeEndType = snakeWork.WorkType;
                    }
                    peroidSt = snakeWork.Get_Start();
                    peroidEd = snakeWork.Get_Finish(true);
                    foodPointer += 1;
                }
            }
        }

        public ElementStatus GetElementStatus(DateTime start, DateTime finish)
        {
            foreach (var phast in this.ElemStausPeroid)
            {
                var pSt = phast.Item1;
                var pEd = phast.Item2;
                var status = phast.Item3;
                if (start > pSt && finish < pEd)
                {
                    return status;
                }
            }
            //if nothing hit, retur active
            return ElementStatus.Active;
        }

        public List<string> GetElementPhaseString()
        {
            List<string> result = new List<string>();
            foreach (var item in this.ElemStausPeroid)
            {
                var st = item.Item1.ToShortDateString();
                var ed = item.Item2.ToShortDateString();
                var phase = item.Item3.ToString();
                result.Add($"{phase}-{st}-{ed}");
            }
            return result;
        }
        public bool IsElementVoidDuringWork(Work work)
        {
            foreach (var peroid in this.ElemStausPeroid)
            {
                var st = peroid.Item1;
                var ed = peroid.Item2;
                var status = peroid.Item3;
                var wkSt = work.Get_Start();
                var wkEd = work.Get_Finish(true);
                if (status == ElementStatus.Void)
                {
                    if (st < wkSt && ed > wkEd)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        public int GetIndex()
        {
            return tempIndex;
        }
        public void UpdateIndex(int index)
        {
            tempIndex = index;
        }
    }

    public class HazardVoxelElementInfo : _4DElement
    {
        public List<HazardVoxel> Voxels { get; set; }
        public HazardVoxelElementInfo(VoxelElement ve, List<Work> works)
        {
            this.ElementId = ve.ElementId;
            List<HazardVoxel> voxels = new List<HazardVoxel>() { Capacity = ve.Voxels.Count };
            this.Voxels = voxels;
            foreach (var v in ve.Voxels)
            {
                HazardVoxel hv = new HazardVoxel();
                hv.ColIndex = v.ColIndex;
                hv.RowIndex = v.RowIndex;
                hv.BottomElevation = v.BottomElevation;
                hv.TopElevation = v.TopElevation;
                this.Voxels.Add(hv);
            }
            SetElementTimeData(works);
        }
    }

    public class HazardMeshElementInfo : _4DElement
    {
        public List<MeshSolid> Solids;
        public double DistanceToFire { get; set; } = double.MaxValue;
        public HazardMeshElementInfo(MeshElement me, List<Work> works)
        {
            this.ElementId = me.ElementId;
            this.Solids = me.Solids;
            SetElementTimeData(works);
        }

        public IEnumerable<Vec3> GetVertices()
        {
            foreach (var sld in this.Solids)
            {
                foreach (var vertex in sld.Vertices)
                    yield return vertex;
            }
        }
        public List<int> GetTriangleIndexes()
        {
            List<int> result = new List<int>();
            foreach (var sld in this.Solids)
            {
                int startOffset = result.Count;
                foreach (var tri in sld.Triangles)
                {
                    foreach (var vi in tri.VerticesIndex)
                    {
                        result.Add(vi + startOffset);
                    }
                }
            }
            return result;
        }
    }

    public class HazardBoxElementInfo : _4DElement
    {
        public List<HazardVoxelBox> Boxes { get; set; }
        public double DistanceToFire { get; set; } = double.MaxValue;
        public bool Ignited { get; set; } = false;

        public HazardBoxElementInfo(LightWeightVoxelElement me, List<Work> works, int voxelSize)
        {
            this.ElementId = me.ElementId;
            this.Boxes = new List<HazardVoxelBox>();
            foreach (var box in me.Boxes)
            {
                var min = box.Min;
                var max = box.Max;
                var boxMin = new CellIndex3D((min.Col) * voxelSize, (min.Row) * voxelSize, min.Layer);
                var boxMax = new CellIndex3D((max.Col + 1) * voxelSize, (max.Row + 1) * voxelSize, max.Layer);
                HazardVoxelBox hazardVoxelBox = new HazardVoxelBox(boxMin, boxMax, this);
                this.Boxes.Add(hazardVoxelBox);
            }

            SetElementTimeData(works);
        }
        /// <summary>
        /// Get the cell projection
        /// </summary>
        /// <param name="voxSize">vox size to split voxels</param>
        /// <returns></returns>
        public IEnumerable<CellIndex> Get2DProjection(int voxSize)
        {
            foreach (var box in this.Boxes)
            {
                var min = box.Min;
                var max = box.Max;
                int colSt = min.Col / voxSize;
                int colEd = max.Col / voxSize - 1;
                int rowSt = min.Row / voxSize;
                int rowEd = max.Row / voxSize - 1;
                for (int col = colSt; col <= colEd; col++)
                {
                    for (int row = rowSt; row <= rowEd; row++)
                    {
                        yield return new CellIndex(col, row);
                    }
                }

            }
        }



        public IEnumerable<CellIndex> Get2DProjectionIntersecing(int voxSize, int bottomeElevInclusive, int topElevationInclusive)
        {
            foreach (var box in this.Boxes)
            {
                var min = box.Min;
                var max = box.Max;
                if (min.Layer <= topElevationInclusive && max.Layer >= bottomeElevInclusive) //inclusion occurs
                {
                    int colSt = min.Col / voxSize;
                    int colEd = max.Col / voxSize - 1;
                    int rowSt = min.Row / voxSize;
                    int rowEd = max.Row / voxSize - 1;
                    for (int col = colSt; col <= colEd; col++)
                    {
                        for (int row = rowSt; row <= rowEd; row++)
                        {
                            yield return new CellIndex(col, row);
                        }
                    }
                }


            }
        }
        public void ResetFireStatus()
        {
            this.Ignited = false;
            foreach (var box in this.Boxes)
            {
                box.VerticalNearFire = false;
            }
        }

    }
    public enum ElementStatus
    {
        Quiescent = 0,
        Active = 1,
        Void = 2
    }

    public class HazardMaterial
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool Combustible { get; set; }
        public HazardMaterial(string id, string name, bool Combustible)
        {
            this.Id = id;
            this.Name = name;
            this.Combustible = Combustible;
        }
    }

    public class LLMClient
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private string apiKey { get; set; }
        private string modelName { get; set; }
        private string modelURL { get; set; }
        private string systemInfo = "你是一个AI助手";

        public LLMClient(string apiKey, string modelName, string modelURL)
        {
            this.apiKey = apiKey;
            this.modelName = modelName;
            this.modelURL = modelURL;
        }
        public void Update(string key, string modelName, string modelURL)
        {
            SetKey(key);
            SetModel(modelName);
            SetEndPoint(modelURL);
        }

        public void SetKey(string key)
        {
            this.apiKey = key;
        }
        public void SetModel(string modelName)
        {
            this.modelName = modelName;
        }
        public void SetEndPoint(string endPoint)
        {
            this.modelURL = endPoint;
        }
        public void SetSystemInfo(string systemInfo)
        {
            this.systemInfo = systemInfo;
        }

        public async Task<Tuple<string, int>> GetAnswer(string userInfo)
        {
            // 若没有配置环境变量，请用百炼API Key将下行替换为：string? apiKey = "sk-xxx";
            if (string.IsNullOrEmpty(apiKey))
            {
                return new Tuple<string, int>("API Key 未设置。请输入正确的APIKey。", 0);

            }
            // 设置请求 URL 和内容
            string url = modelURL;

            // 此处以qwen-plus为例，可按需更换模型名称。模型列表：https://help.aliyun.com/zh/model-studio/getting-started/models
            var request = new
            {
                model = this.modelName, // 根据文档选择模型
                messages = new[]
                {
                    new { role = "system", content = systemInfo },
                    new { role = "user", content = userInfo}
                },
                temperature = 0,
                //max_tokens = 1500
            };
            string jsonContent = JsonConvert.SerializeObject(request);
            // 发送请求并获取响应
            var result = await SendPostRequestAsync(url, jsonContent, apiKey);
            // 输出结果
            return result;
        }

        private async Task<Tuple<string, int>> SendPostRequestAsync(string url, string jsonContent, string apiKey)
        {
            using (var content = new StringContent(jsonContent, Encoding.UTF8, "application/json"))
            {
                // 设置请求头
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                // 发送请求并获取响应
                HttpResponseMessage response = await httpClient.PostAsync(url, content);
                // 处理响应
                if (response.IsSuccessStatusCode)
                {
                    var rawResponse = await response.Content.ReadAsStringAsync();
                    dynamic doc = JsonConvert.DeserializeObject(rawResponse);
                    string reply = doc.choices[0].message.content;
                    int totalToken = doc.usage.total_tokens;
                    return new Tuple<string, int>(reply.Replace("\n", Environment.NewLine), totalToken);
                    //return fullResponse.ToString();
                }
                else
                {
                    return new Tuple<string, int>($"请求失败: {response.StatusCode}", 0);
                }
            }
        }
    }
    public class StreamResponse
    {
        public string Model { get; set; }
        public string Response { get; set; }
        public bool Done { get; set; }
        public long TotalDuration { get; set; }
    }

    public class HazardElementChunk
    {
        public List<HazardVoxelElementInfo> IgnitionSources { get; set; } = new List<HazardVoxelElementInfo>();
        public List<HazardCombination> Combinations { get; set; } = new List<HazardCombination>();
        public HazardElementChunk(List<HazardVoxelElementInfo> ignitionSource, List<HazardCombination> combos)
        {
            this.IgnitionSources = ignitionSource;
            this.Combinations = combos;
        }

    }
    public class HazardCombination
    {
        public string Name { get; set; }
        public Work IngntionSource { get; set; }
        public List<Work> CombustibleWorks { get; set; } = new List<Work>();
        public List<HazardMaterial> CombustibleMaterials { get; } = new List<HazardMaterial>();
        public double Distance { get; set; } = double.MaxValue;
        public CombinationHazardLevel HazardLevel { get; set; }
        public HazardCombination()
        {

        }
        /// <summary>
        /// Use this constructor only when the element is vaid under ignitionsource
        /// </summary>
        /// <param name="element"></param>
        /// <param name="ignitionSource"></param>
        public HazardCombination(_4DElement element, Work ignitionSource)
        {
            var fireSt = ignitionSource.Get_Start();
            var fireEd = ignitionSource.Get_Finish(true);
            var workAttatched = element.Works;
            this.HazardLevel = CombinationHazardLevel.High;
            this.IngntionSource = ignitionSource;
            foreach (var mat in element.Materials)
            {
                if (mat.Combustible)
                {
                    this.HazardLevel = CombinationHazardLevel.Extreme;
                    this.CombustibleMaterials.Add(mat);
                }
            }
            foreach (var work in workAttatched)
            {
                var st = work.Get_Start();
                var fn = work.Get_Finish(true);
                if (fireSt <= fn && fireEd >= st) //work inteersect
                {
                    if (work.Combustible || work.EmitGas)
                    {
                        this.CombustibleWorks.Add(work);
                        this.HazardLevel = CombinationHazardLevel.Extreme;
                    }
                }
            }
        }
        /// <summary>
        /// make sure use this constructor to creeaete a non-under-fire combos
        /// </summary>
        /// <param name="element"></param>
        public HazardCombination(_4DElement element)
        {
            var workAttatched = element.Works;
            this.HazardLevel = CombinationHazardLevel.Low;
            foreach (var mat in element.Materials)
            {
                if (mat.Combustible)
                {
                    this.HazardLevel = CombinationHazardLevel.Medium;
                    this.CombustibleMaterials.Add(mat);
                }
            }
            foreach (var work in workAttatched)
            {
                if (work.Combustible || work.EmitGas)
                {
                    this.CombustibleWorks.Add(work);
                    this.HazardLevel = CombinationHazardLevel.Medium;
                }
            }

        }
        public string Get_Description()
        {
            StringBuilder result = new StringBuilder();
            result.Append("Ingition Source:");
            List<string> ignitionName = new List<string>();
            var wk = this.IngntionSource;
            ignitionName.Add($"{wk.Id}_{wk.Name}_{wk.Get_Start().ToShortDateString()}_{wk.Get_Finish(true).ToShortDateString()}");
            List<string> combMats = new List<string>();
            foreach (var mat in this.CombustibleMaterials)
            {
                combMats.Add($"{mat.Id}_{mat.Name}");
            }
            List<string> combWorks = new List<string>();
            foreach (var cw in this.CombustibleWorks)
            {
                combWorks.Add($"{cw.Id}_{cw.Name}_{cw.Get_Start().ToShortDateString()}_{cw.Get_Finish(true).ToShortDateString()}");
            }

            if (ignitionName.Count > 0)
            {
                result.Append(string.Join(";", ignitionName));

            }
            else
            {
                result.Append("Empty");
            }
            result.Append("\r\n");
            result.Append("Combustible materials:");
            if (combMats.Count > 0)
            {
                result.Append(string.Join(";", combMats));
            }
            else
            {
                result.Append("Empty");
            }
            result.Append("\r\n");
            result.Append("Combustible Works:");
            if (combWorks.Count > 0)
            {
                result.Append(string.Join(";", combWorks));
            }
            else
            {
                result.Append("Empty");
            }
            result.Append($"\r\nHazard Level:{this.HazardLevel.ToString()}");
            return result.ToString();

        }
        public string Get_ShortDescription()
        {
            string strIgnition = "INVALID";
            if (this.IngntionSource != null)
                strIgnition = this.IngntionSource.Id;
            string strCombWk = "INVALID";
            if (this.CombustibleWorks.Count > 0)
            {
                List<string> combIds = new List<string>();
                foreach (var c in this.CombustibleWorks)
                {
                    combIds.Add(c.Id);
                }
                strCombWk = string.Join(";", combIds);
            }

            string strCombMat = "INVALID";
            if (this.CombustibleMaterials.Count > 0)
            {
                List<string> combIds = new List<string>();
                foreach (var c in this.CombustibleMaterials)
                {
                    combIds.Add(c.Id);
                }
                strCombMat = string.Join(";", combIds);
            }

            return $"I:{strIgnition}_CW:{strCombWk}_CM:{strCombMat}_Lv:{this.HazardLevel.ToString()}";

        }
    }
    public enum CombinationHazardLevel
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Extreme = 3
    }

    public class FireSearchRange2D
    {
        public double SearchRange { get; set; }
        public double SearchInteval { get; set; }
        private int RangeRadius { get; set; }
        private int RangeTilt { get; set; }
        private List<CellIndex> RangeENWS { get; set; }
        private List<CellIndex> RangeNE_NW_SW_SE { get; set; }
        private List<CellIndex> RangeFanCCW { get; set; }
        private int phaseCount;
        public FireSearchRange2D(double searchRange, double searchInteval)
        {
            SearchRange = searchRange;
            SearchInteval = searchInteval;
            RangeRadius = (int)Math.Ceiling(Math.Round(searchRange / searchInteval, 3));
            FindElementsWithin2DRange();
        }

        public IEnumerable<CellIndex> GetAllCellIndex()
        {
            yield return new CellIndex(0, 0);
            foreach (var cix in RangeENWS)
            {
                yield return cix;
            }
            foreach (var cix in RangeNE_NW_SW_SE)
            {
                yield return cix;
            }
            foreach (var cix in RangeFanCCW)
            {
                yield return cix;
            }
        }
        public void FindElementsWithin2DRange()
        {
            RangeENWS = new List<CellIndex>();
            RangeNE_NW_SW_SE = new List<CellIndex>();
            RangeFanCCW = new List<CellIndex>();
            var maxSearchDistance = this.SearchRange;

            int rangeSizeMax = RangeRadius;
            var rangeSquare = Math.Pow(SearchRange, 2);
            // update ENWS
            for (int col = 1; col <= rangeSizeMax; col++)
            {
                RangeENWS.Add(new CellIndex(col, 0));
                RangeENWS.Add(new CellIndex(0, col));
                RangeENWS.Add(new CellIndex(-col, 0));
                RangeENWS.Add(new CellIndex(0, -col));
            }
            // add  RangeNE_NW_SW_SE and RangeFan

            for (int colAbs = 1; colAbs <= rangeSizeMax; colAbs++)
            {
                for (int rowAbs = 1; rowAbs <= colAbs; rowAbs++)
                {
                    var colOffLoc = colAbs - 1;
                    var rowOffLoc = Math.Max(0, rowAbs - 1);
                    var disLocSquare = (Math.Pow(colOffLoc, 2) + Math.Pow(rowOffLoc, 2)) * Math.Pow(this.SearchInteval, 2);
                    if (disLocSquare < rangeSquare)
                    {
                        if (colAbs > rowAbs)
                        {
                            RangeFanCCW.Add(new CellIndex(colAbs, rowAbs));
                            RangeFanCCW.Add(new CellIndex(rowAbs, colAbs));
                            RangeFanCCW.Add(new CellIndex(-rowAbs, colAbs));
                            RangeFanCCW.Add(new CellIndex(-colAbs, rowAbs));
                            RangeFanCCW.Add(new CellIndex(-colAbs, -rowAbs));
                            RangeFanCCW.Add(new CellIndex(-rowAbs, -colAbs));
                            RangeFanCCW.Add(new CellIndex(rowAbs, -colAbs));
                            RangeFanCCW.Add(new CellIndex(colAbs, -rowAbs));
                        }
                        else
                        {
                            RangeNE_NW_SW_SE.Add(new CellIndex(colAbs, rowAbs));
                            RangeNE_NW_SW_SE.Add(new CellIndex(-colAbs, rowAbs));
                            RangeNE_NW_SW_SE.Add(new CellIndex(-colAbs, -rowAbs));
                            RangeNE_NW_SW_SE.Add(new CellIndex(colAbs, -rowAbs));
                        }
                    }

                }
            }
            RangeTilt = RangeNE_NW_SW_SE.Count / 4;
            phaseCount = RangeFanCCW.Count / 8;
        }

        public IEnumerable<CellIndex> GetFanAfterDiection(RangeDirection rd)
        {
            int idx0 = (int)rd;
            for (int i = idx0; i <= this.RangeFanCCW.Count - 1; i += 8)
            {
                yield return this.RangeFanCCW[i];
            }
        }
        public IEnumerable<CellIndex> GetCellsOfDirection(RangeDirection rd)
        {
            switch (rd)
            {
                case RangeDirection.E:
                    for (int i = 0; i < RangeENWS.Count; i += 4)
                        yield return this.RangeENWS[i];
                    break;
                case RangeDirection.N:
                    for (int i = 1; i < RangeENWS.Count; i += 4)
                        yield return this.RangeENWS[i];
                    break;
                case RangeDirection.W:
                    for (int i = 2; i < RangeENWS.Count; i += 4)
                        yield return this.RangeENWS[i];
                    break;
                case RangeDirection.S:
                    for (int i = 3; i < RangeENWS.Count; i += 4)
                        yield return this.RangeENWS[i];
                    break;
                case RangeDirection.NE:
                    for (int i = 0; i < RangeNE_NW_SW_SE.Count; i += 4)
                        yield return this.RangeNE_NW_SW_SE[i];
                    break;
                case RangeDirection.NW:
                    for (int i = 1; i < RangeNE_NW_SW_SE.Count; i += 4)
                        yield return this.RangeNE_NW_SW_SE[i];
                    break;
                case RangeDirection.SW:
                    for (int i = 2; i < RangeNE_NW_SW_SE.Count; i += 4)
                        yield return this.RangeNE_NW_SW_SE[i];
                    break;

                case RangeDirection.SE:
                    for (int i = 3; i < RangeNE_NW_SW_SE.Count; i += 4)
                        yield return this.RangeNE_NW_SW_SE[i];
                    break;
            }
        }
        public IEnumerable<CellIndex> GetCellBetween(RangeDirection start, RangeDirection end, bool includeStart, bool IncludeEnd)
        {
            int idx0 = (int)start;
            int idx1 = (int)end;
            if (idx1 < idx0)
                idx1 += 8;
            //add st
            if (includeStart)
            {
                foreach (var cell in GetCellsOfDirection(start))
                {
                    yield return cell;
                }
            }
            //add first fun
            foreach (var cix in GetFanAfterDiection(start))
            {
                yield return cix;
            }
            //add other voxels
            for (int idx = idx0 + 1; idx <= idx1 - 1; idx++)
            {
                var curIdx = idx % 8;
                var dir = (RangeDirection)(curIdx);
                //add axis
                foreach (var cell in GetCellsOfDirection(dir))
                {
                    yield return cell;
                }
                //add fan
                foreach (var cix in GetFanAfterDiection(dir))
                {
                    yield return cix;
                }
            }
            //add end
            if (IncludeEnd)
            {
                foreach (var cell in GetCellsOfDirection(end))
                {
                    yield return cell;
                }
            }
        }
    }
    public enum RangeDirection
    {
        E = 0,
        NE = 1,
        N = 2,
        NW = 3,
        W = 4,
        SW = 5,
        S = 6,
        SE = 7



    }
}
