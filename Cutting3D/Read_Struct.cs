using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EvilDICOM.Core;
using EvilDICOM.Core.Selection;
using EvilDICOM.CV.Helpers;
using EvilDICOM.CV.RT.Meta;
using OpenCvSharp;

namespace Cutting3D
{
    public class Read_Struct
    {
        public Read_Struct()
        {
            
        }
        public List<KeyValuePair<string, StructureMeta>> GetStructList(string File_Path)
        {
            DICOMSelector selector = DICOMObject.Read(File_Path).GetSelector();
            StructureSetMeta structureSetMeta = new();
            if (selector.Modality.Data != "RTSTRUCT") { return null; }

            foreach (StructureMeta meta in selector.StructureSetROISequence.Items.Select((DICOMObject i) => new StructureMeta
            {
                StructureId = i.GetSelector().ROIName?.Data,
                ROINumber = i.GetSelector().ROINumber.Data
            }))
            {
                try
                {
                    DICOMObject dICOMObject = selector.ROIContourSequence.Items.FirstOrDefault((DICOMObject i) => i.GetSelector().ReferencedROINumber.Data == meta.ROINumber);
                    DICOMObject? dICOMObject2 = selector.RTROIObservationsSequence.Items.FirstOrDefault((DICOMObject i) => i.GetSelector().ReferencedROINumber.Data == meta.ROINumber);
                    List<int> data_ = dICOMObject.GetSelector().ROIDisplayColor.Data_;
                    new Vec3b((byte)data_[0], (byte)data_[1], (byte)data_[2]);
                    string data = dICOMObject2!.GetSelector().RTROIInterpretedType.Data;
                    string data2 = (selector.ROIObservationLabel != null) ? selector.ROIObservationLabel.Data : " ";

                    meta.StructureName = data2;
                    meta.Color = new Scalar(data_[0], data_[1], data_[2]);
                    if (dICOMObject.GetSelector().ContourSequence == null)
                    {
                        continue;
                    }

                    foreach (DICOMObject item in dICOMObject.GetSelector().ContourSequence.Items)
                    {
                        List<double> data_2 = item.GetSelector().ContourData.Data_;
                        if (data_2.Count % 3 != 0)
                        {
                            //Logger.Error($"Slice for structure {meta.StructureId} has {data_2.Count} contour points. Not divisible by 3! Can't process.");
                            continue;
                        }

                        try
                        {
                            SliceContourMeta sliceContourMeta = new SliceContourMeta();
                            for (int j = 0; j < data_2.Count; j += 3)
                            {
                                Point3f pt = new Point3f((float)data_2[j], (float)data_2[j + 1], (float)data_2[j + 2]);
                                sliceContourMeta.AddPoint(pt);
                            }

                            meta.SliceContours.Add(sliceContourMeta);
                            meta.DICOMType = data;
                        }
                        catch (Exception ex)
                        {
                            //Logger.Error(ex.Message);
                            Console.WriteLine(ex.ToString());
                        }
                    }

                    foreach (IGrouping<float, SliceContourMeta> item2 in (from s in meta.SliceContours
                                                                          group s by s.Z).ToList())
                    {
                        List<SliceContourMeta> list = item2.OrderByDescending((SliceContourMeta s) => s.CalculateArea()).ToList();
                        ContourHelper.OrganizeIntoChildren(list[0], list.Skip(1));
                    }
                    structureSetMeta.Structures.Add(meta.StructureId, meta);
                }
                catch (Exception)
                {
                    //Logger.Error(exception.Message + "Could not add structure " + meta.StructureId.ToString());
                }
            }

            if (structureSetMeta.Structures.Values.First().SliceContours.Count > 0)
            {
                return structureSetMeta.Structures.ToList();
            }
            else { return null; }

        }
    }
}
