using OperationalAgentRuntime.Models;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OperationalAgentRuntime.Helpers
{
    public static class ChartHelper
    {
        public static string GenerateChartBase64String(ChartImageInput chartImageInput)
        {
            if (chartImageInput.TimeSeries == null || !chartImageInput.TimeSeries.Any())
                return string.Empty;

            // Extract data for plotting  
            DateTime[] dts = chartImageInput.TimeSeries.Select(data => data.Timestamp).ToArray();
            double[] ys = chartImageInput.TimeSeries.Select(data => data.Value).ToArray();

            // Create a new ScottPlot plot  
            var plt = new Plot();

            // Plot data  
            plt.Add.Scatter(dts, ys);
            plt.Axes.DateTimeTicksBottom();

            // Set plot title and labels 
            if (!string.IsNullOrWhiteSpace(chartImageInput.Title))
                plt.Title(chartImageInput.Title);
            
            if(!string.IsNullOrWhiteSpace(chartImageInput.YAxisLabel))
                plt.YLabel(chartImageInput.YAxisLabel);

            plt.XLabel("Time (UTC)");
            if (chartImageInput.YAxisMax.GetValueOrDefault(0.0) > 0.0)
            {
                plt.Axes.SetLimitsY(bottom: chartImageInput.YAxisMin.GetValueOrDefault(0.0), top: chartImageInput.YAxisMax.GetValueOrDefault(0.0));
            }

            var imageFile = $"{Guid.NewGuid()}.png";

            if (File.Exists(imageFile))
                File.Delete(imageFile);

            var savedImage = plt.SavePng(imageFile, 600, 400);
            string base64 = ConvertImageToBase64String(imageFile);
            File.Delete(imageFile);

            return base64;
        }

        private static string ConvertImageToBase64String(string imagePath)
        {
            try
            {
                // Read the image file into a byte array  
                byte[] imageBytes = File.ReadAllBytes(imagePath);

                // Convert the byte array to a Base64 string  
                string base64String = Convert.ToBase64String(imageBytes);

                return $"data:image/png;base64,{base64String}";
            }
            catch (Exception ex)
            {
                // TODO : need to log exception
                return string.Empty;
            }
        }

    }
}
