using Amazon.DynamoDBv2.DataModel;
using System;
using System.Collections.Generic;

namespace Surya_COMP306Lab4.Models
{
    [DynamoDBTable("ImageProcessing")]
    public class ImageProcessing
    {
        [DynamoDBProperty("ImageID")]
        [DynamoDBHashKey]
        public string ImageID { get; set; }
        [DynamoDBProperty("ImageName")]        
        public string ImageName { get; set; }

        [DynamoDBProperty("Label")]
        public string Label { get; set; }

        [DynamoDBProperty("Confidence")]
        public string Confidence { get; set; }

       
    }
}
