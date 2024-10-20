﻿using Amazon.S3;
using Amazon.S3.Model;
using Estranged.Lfs.Data;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Estranged.Lfs.Adapter.S3
{
    internal sealed class S3BlobAdapter : IBlobAdapter
    {
        private readonly IAmazonS3 client;
        private readonly IS3BlobAdapterConfig config;

        public S3BlobAdapter(IAmazonS3 client, IS3BlobAdapterConfig config)
        {
            this.client = client;
            this.config = config;
        }

        public Uri MakePreSignedUrl(string oid, HttpVerb verb, string mimeType, long? size = null)
        {
            var request = new GetPreSignedUrlRequest
            {
                Verb = verb,
                BucketName = config.Bucket,
                Key = config.KeyPrefix + oid,
                Protocol = Protocol.HTTPS,
                ContentType = mimeType,
                Expires = DateTime.UtcNow + config.Expiry
            };
            if (size.HasValue && config.IntelligentTieringMinSize >= 0 && size > config.IntelligentTieringMinSize)
            {
                request.Parameters.Add("x-amz-storage-class", "INTELLIGENT_TIERING");
            }
            return new Uri(client.GetPreSignedURL(request));
        }

        public async Task<SignedBlob> UriForDownload(string oid, CancellationToken token)
        {
            GetObjectMetadataResponse metadataResponse;
            try
            {
                metadataResponse = await client.GetObjectMetadataAsync(config.Bucket, config.KeyPrefix + oid, token).ConfigureAwait(false);
            }
            catch (AmazonS3Exception ex)
            {
                return new SignedBlob
                {
                    ErrorCode = (int)ex.StatusCode,
                    ErrorMessage = ex.Message
                };
            }

            return new SignedBlob
            {
                Uri = MakePreSignedUrl(oid, HttpVerb.GET, null),
                Size = metadataResponse.ContentLength,
                Expiry = config.Expiry
            };
        }

        public Task<SignedBlob> UriForUpload(string oid, long size, CancellationToken token)
        {
            return Task.FromResult(new SignedBlob
            {
                Uri = MakePreSignedUrl(oid, HttpVerb.PUT, BlobConstants.UploadMimeType, size),
                Expiry = config.Expiry,
                Headers = new Dictionary<string, string>
                {
                    {"Content-Type", BlobConstants.UploadMimeType}
                }
            });
        }
    }
}
