using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.Lambda.Core;

using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3;
using Amazon.S3.Model;
using static Amazon.S3.Util.S3EventNotification;
using Image = Amazon.Rekognition.Model.Image;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.Runtime;
using Surya_COMP306Lab4.Models;
// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Surya_COMP306Lab4
{
    public class StepFunctionTasks
    {
        IAmazonS3 S3Client { get; }
        IAmazonRekognition RekognitionClient { get; }
        public DynamoDBContext Dbcontext { get; }

        public static AmazonDynamoDBClient client = null;
        public BasicAWSCredentials credentials { get; }
        HashSet<string> SupportedImageTypes { get; } = new HashSet<string> { ".png", ".jpg", ".jpeg" };

        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public StepFunctionTasks()
        {
            var credentials = new BasicAWSCredentials("AKIA4222HWYM4Z7JDGAM", "Rith5hH4hWnMOsvFKBMo8bTnfGcoidtaBY2Z9P5S");
            this.S3Client = new AmazonS3Client(credentials, Amazon.RegionEndpoint.USEast1);
            this.RekognitionClient = new AmazonRekognitionClient(credentials, Amazon.RegionEndpoint.USEast1);
            client = new AmazonDynamoDBClient(credentials, Amazon.RegionEndpoint.USEast1);
            Dbcontext = new DynamoDBContext(client);
        }

        public State IsUploadedObjectAnImage(State state, ILambdaContext context)
        {
            if (!SupportedImageTypes.Contains(Path.GetExtension(state.Key)))
            {
                state.IsAnImage = false;
            }
            else
            {
                state.IsAnImage = true;
            }

            return state;
        }

       

        public State GenerateThumbnail(State state, ILambdaContext context)
        {
            try
            {
                LambdaLogger.Log("First");
                CreateThumbnail(state);
                return state;
            }
            catch (Exception e)
            {
                LambdaLogger.Log(e.StackTrace.ToString());
                throw;
            }
            
        }

        public State DetectLabel(State state, ILambdaContext context)
        {
            var detectResponses = this.RekognitionClient.DetectLabelsAsync(new DetectLabelsRequest
            {
                MinConfidence = 90,
                Image = new Image
                {
                    S3Object = new Amazon.Rekognition.Model.S3Object
                    {
                        Bucket = state.BucketName,
                        Name = state.Key
                    }
                }
            }).Result;
            var tags = new List<Tag>();
            var imageProcessings = new List<ImageProcessing>();
            foreach (var label in detectResponses.Labels)
            {
                tags.Add(new Tag { Key = label.Name, Value = label.Confidence.ToString() });
                imageProcessings.Add(new ImageProcessing { Label = label.Name, Confidence = label.Confidence.ToString(), ImageName = state.Key, ImageID = Guid.NewGuid().ToString() });

            }
            var bookBatch = Dbcontext.CreateBatchWrite<ImageProcessing>();
            bookBatch.AddPutItems(imageProcessings);
            bookBatch.ExecuteAsync();
            _ = this.S3Client.PutObjectTaggingAsync(new PutObjectTaggingRequest
            {
                BucketName = state.BucketName,
                Key = state.Key,
                Tagging = new Tagging
                {
                    TagSet = tags
                }
            }).Result;

            return state;
        }

        private string CreateThumbnail(State state)
        {
          
                var rs = S3Client.GetObjectMetadataAsync(
                    state.BucketName,
                    state.Key).Result;

                if (rs.Headers.ContentType.StartsWith("image/"))
                {
                    using (GetObjectResponse response = S3Client.GetObjectAsync(
                        state.BucketName,
                        state.Key).Result)
                    {
                        using (Stream responseStream = response.ResponseStream)
                        {
                            using (StreamReader reader = new StreamReader(responseStream))
                            {
                                using (var memstream = new MemoryStream())
                                {
                                    var buffer = new byte[512];
                                    var bytesRead = default(int);
                                    while ((bytesRead = reader.BaseStream.Read(buffer, 0, buffer.Length)) > 0)
                                        memstream.Write(buffer, 0, bytesRead);
                                    // Perform image manipulation 
                                    var transformedImage = GcImagingOperations.GetConvertedImage(memstream.ToArray());

                                    PutObjectRequest putRequest = new PutObjectRequest()
                                    {
                                        BucketName = state.BucketName,
                                        Key = $"thumbnail/{state.Key}",
                                        ContentType = rs.Headers.ContentType,
                                        InputStream = transformedImage
                                    };

                                    _ = S3Client.PutObjectAsync(putRequest).Result;
                                }
                            }
                        }
                    }
                }
                return rs.Headers.ContentType;
           
        }
    }
}
