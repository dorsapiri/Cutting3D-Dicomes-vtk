using Kitware.VTK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cutting3D
{
    internal class DicomSlice
    {
        public int sliceId;
        public List<List<OpenCvSharp.Point2f>> skins;
        public float position;
        public vtkImageData imageData;
        public vtkPolyData polyLine;
        public vtkImageData croppedImageData;
    }
}
