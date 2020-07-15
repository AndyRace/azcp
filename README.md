# Why use AzCp instead of AzCopy or Azure Storage Explorer?

Unlike [Azure Storage Explorer](https://azure.microsoft.com/en-us/features/storage-explorer/) and [AzCopy](https://docs.microsoft.com/en-us/azure/storage/common/storage-use-azcopy-v10?toc=/azure/storage/blobs/toc.json) I want to be able to resume partially uploaded files to Azure BLOB storage.

Both [Azure Storage Explorer](https://azure.microsoft.com/en-us/features/storage-explorer/) and [AzCopy](https://docs.microsoft.com/en-us/azure/storage/common/storage-use-azcopy-v10?toc=/azure/storage/blobs/toc.json) have **significantly** more options that this tool!  They're great tools, I'm sure this gap will eventually be filled, but until then there's AzCp.

NOTE: Both resume the file *set* (aka Job) but do **not** resume a partially uploaded file - hence AzCp.
So if you have successfully uploaded 90% of your multi-Terrabyte file and AzCopy is closed, re-starting AzCopy with the interrupted Job will skip files that have been uploaded already but it will start the incomplete file **from the start!**.

## Overview

1. AzCp monitors a specified folder for additional files (Uploads)
2. AzCp copies these files into Azure BLOB Storage using a pre-provided SAS Token
3. AzCp archives the files once uploaded - moves them to another folder (Archive)
4. AzCp resumes the upload if the tool is interrupted and restarted

## Installing AzCp

1. Copy the AzCp.exe and appSettings.json files to a folder of your choice
2. Create an appSettings.secrets.json file in the same folder as the .exe with contents of the form:

``` json
{
  "Repository": {
    // The 'BlobContainerUri' entry contains Shared Access Signature URI that you got from Azure Storage Explorer (for example)
    // It is of the form:
    //   "BlobContainerUri": "https://{storage account name}.blob.core.windows.net/{container name}?{SAS query string}"
  }
}
```

## Running AzCp

The following assumes you are using the defaults in [appSettings.json](test/appSettings.json).  Hopefully the comments in this file indicates what you are able to change and why.

* Run the AzCp console application
* Copy or move files into the ***Uploads*** sub-folder
* AzCp will copy all of these files - respecting any sub-folder structure - into the storage container specified in appSettings.secrets.json
* Once copied, the file is archived to the **Archive** sub-folder

## Build status

![build](https://github.com/AndyRace/azcp/workflows/build/badge.svg)
