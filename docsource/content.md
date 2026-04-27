## Overview

The Google Cloud Platform (GCP) Secret Manager Orchestrator Extension remotely manages certificates stored as secrets in Google Cloud's Secret Manager.  Each certificate store set up in Keyfactor Command represents a Google Cloud project.  This orchestrator extension supports the inventory and management of certificates in PEM format stored as secrets and supports the following use cases:

* PEM encoded certificate and unencrypted or encrypted private key
* PEM encoded certificate and unencrypted or encrypted private key with full certificate chain
* PEM encoded certificate only

Additional features:
* For use cases including an encrypted private key, please refer to [Certificate Encryption Details](#certificate-encryption-details) for more information on handling/storing the encryption password for the private key.
* For information on Tag Support, please refer to [Tag Support](#tag-support)
* For information on Label Support, please refer to [Label Support](#label-support)
* For information on Automatic vs User Managed Replication, please refer to [Region Replication](#region-replication)
* For information on Secret and Secret Version Retention, please refer to [TTL and TTL Version Retention](#ttl-and-ttl-version-retention)


## Requirements

The GCP Secret Manager Orchestrator Extension uses Google Application Default Credentials (ADC) for authentication.  Testing of this orchestrator extension was performed using a service account, but please review [Google Application Default Credentials](https://cloud.google.com/docs/authentication/application-default-credentials) for more information on the various ways authentication can be set up.

The GCP project and account being used to access Secret Manager must have access to and enabled the Secret Manger API and also must have assigned to it the following roles:
* Secret Manager Admin
* Tag User (if assigning tags to secrets)
* Folder Viewer (if assigning tags to secrets AND the project assigned for this certificate store has a folder as a direct parent)
* Cloud KMS CryptoKey Encrypter/Decrypter (If assigning KMS Paths to regions when adding secrets using user managed replication)


## Certificate Encryption Details

For GCP Secret Manager secrets containing private keys, the GCP Secret Manager Orchestrator Extension provides three ways to manage the certificate private key:

1. Using the Keyfactor Command Store Password on the certificate store definition to store the password that will be used to encrypt ALL private keys for the GCP Secret Manager project.
2. Using the Password Secret Location Suffix field on the certificate store definition to store a "suffix" that will be used in conjunction with the secret alias (name) to create a second secret in Secret Manager to store the encryption password.
3. If no Store Password is set and the Password Secret Location Suffix is either missing or blank, the private key will not be encrypted.

If the Store Password has a value, this will be used to encrypt the private key during a Management Add job.  If no value is set for the Store Password, the one time password that Keyfactor Command generates when triggering a Management-Add job will be used to encrypt the private key and this password will be stored as a secret in GCP Secret Manager with a name of Alias + Password Secret Location Suffix.  For example, if the certificate alias is set as "Alias1" and the Password Secret Location Suffix is set as "_Key", the certificate and encrypted private key will be stored in a secret named "Alias1" and the password for the key encryption will be stored in a secret named "Alias1_Key".  Please note that if using the generated password Keyfactor Command provides and storing the password in Secret Manager, each renewal/replacement of a certificate will encrypt the private key with a new generated password, which will then be stored as a new version of the password secret.


## Tag Support

This extension supports the management of secret tags.  **If** the optional Entry Parameter "Tags" exists in the store type definition:
* Inventory will return all tags assigned to a secret in the comma delimited format of "TagKey1:TagValue1,TagKey2:TagValue2,...,TagKeyN:TagValueN".  
* The same format of one-to-many tag key/value pairs ("TagKey1:TagValue1,TagKey2:TagValue2,...,TagKeyN:TagValueN") can be added to the "Tags" field during the setup of Management-Add jobs to assign tags to the secret **as long as each tag key/value pair is already set up as a valid Organization level tag key/value combination in GCP**.

Additional notes regarding tags:
* This integration does **not** support Project level tags when adding new certificates (secrets).  Only Organization level tags will be recognized.
* The Tags field will be ignored when renewing/replacing a certificate since in this scenario the extension is only adding a new secret version and not replacing the entire secret.  Assigning tags is only attempted when adding a completely new certificate (secret).
* When adding a new secret, any errors attempting to add tags **will not** impact the adding of the secret.  If a Management-Add job successfully adds a new certificate (secret) but fails to assign the tag, the job will be reported back with a status of Warning along with detailed messages why each tag could not be assigned.  The certificate (secret) itself, however, **will** be added.
* If multiple tags are provided, and errors occur on some but not others, the successful ones will be assigned to the certificate (secret) and warning messages will be written to the log and job status for the others.


## Label Support

This extension supports the management of secret labels.  **If** the optional Entry Parameter "Labels" exists in the store type definition:
* Inventory will return all labels attached to each secret in the comma delimited format of "LabelName1:LabelValue1,LabelName2:LabelValue2,...,LabelNameN:LabelValueN"
* The same format of one-to-many label name/value pairs can be added to new secrets during Management-Add jobs in the Labels Entry Parameter.  These values can also be modified when renewing/replacing an existing secret.

Additional notes regarding labels:
* A blank Labels Entry Parameter will not remove any existing labels from an existing secret being replaced (certificate renewal use case).
* A non blank Labels Entry Parameter will cause all pre-existing labels to be removed and replaced for the secret being replaced.
* Improperly formatted Labels may cause pre-existing labels to be removed but none or only valid ones added, but this will not prevent the secret from being added/replaced.


## Region Replication

This extension supports replicating secrets to one-to-many valid GCP regions, along with optionally specifying a valid GCP Key Management Service (KMS) patha for each region.  **If** the optional Entry Parameter "Replication Regions" exists in the store type definition:
* Inventory will return all replication regions and KMS paths attached to each secret in the comma delimited format of "Region1:KMSPath1,Region2:KMSPath2,...,RegionN:KMSPathN"
* The same format of one-to-many region/KMS path pairs can be added to new secrets during Managment-Add jobs in the Replication Regions Entry Parameter.  Modification of these values is **NOT** supported when renewing/replacing an existing secret.  Region/KMS values entered when renewing/replacing a certificate in Management-Add job will be ignored.
* Replication regions without KMS paths can also be provided - i.e. "Region1,Region2,...,RegionN", but GCP enforces the convention that **all** supplied regions must have an associated valid KMS path or all of them must **not** have a KMS path.  Cannot mix some with and some without.

Additionsl notes regarding replication regions:
* Each region must be a [GCP allowed region for secrets](https://docs.cloud.google.com/secret-manager/docs/locations).
* In order to apply KMS paths to replication regions:
	- A valid KMS Key Ring and Crypto Key must be created for the applicable region.
	- The Cloud KMS CryptoKey Encrypter/Decrypter role must be applied to the service-{PROJECT ID}@gcp-sa-secretmanager.iam.gserviceaccount.com service principle where {PROJECT ID} is the numeric project id for the project the secret is being added to.
	- The Cloud Key Management Service API must be enabled.


## TTL and TTL Version Retention

This extension supports supplying TTL (Time To Live) and Destroy Version TTL values.  **If** the optional Entry Parameters of "TTL Duration" and "Version Destroy TTL Duration" exist in the store type definition:
* A numeric value (in days) can be entered for either or both values specifying when a secret will be deleted (TTL Duration) and how many days after secret deletion each version will be destroyed (Version Destroy TTL Duration).
* These values will be returned in Inventory jobs as well as modified when renewing/replacing a secret.
* Blank values supplied during Management job when replacing a secret will not affect current values for the secret.