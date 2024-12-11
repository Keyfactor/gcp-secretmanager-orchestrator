## Overview

The Google Cloud Platform (GCP) Secret Manager Orchestrator Extension remotely manages certificates stored as secrets in Google Cloud's Secret Manager.  Each certificate store set up in Keyfactor Command represents a Google Cloud project.  This orchestrator extension supports the inventory and management of certificates in PEM format stored as secrets and supports the following use cases:

* PEM encoded certificate and unencrypted or encrypted private key
* PEM encoded certificate and unencrypted or encrypted private key with full certificate chain
* PEM encoded certificate only

For use cases including an encrypted private key, please refer to [Certificate Encryption Details](#certificate-encryption-details) for more information on handling/storing the encryption password for the private key.

## Requirements

The GCP Secret Manager Orchestrator Extension uses Google Application Default Credentials (ADC) for authentication.  Testing of this orchestrator extension was performed using a service account, but please review [Google Application Default Credentials](https://cloud.google.com/docs/authentication/application-default-credentials) for more information on the various ways authentication can be set up.

The GCP project and account being used to access Secret Manager must have access to and enabled the Secret Manger API and also must have assigned to it the Secret Manager Admin role.


## Certificate Encryption Details

For GCP Secret Manager secrets containing private keys, the GCP Secret Manager Orchestrator Extension provides three ways to manage the certificate private key:

1. Using the Keyfactor Command Store Password on the certificate store definition to store the password that will be used to encrypt ALL private keys for the GCP Secret Manager project.
2. Using the Password Secret Location Suffix field on the certificate store definition to store a "suffix" that will be used in conjunction with the secret alias (name) to create a second secret in Secret Manager to store the encryption password.
3. If no Store Password is set and the Password Secret Location Suffix is either missing or blank, the private key will not be encrypted.

If the Store Password has a value, this will be used to encrypt the private key during a Management Add job.  If no value is set for the Store Password, the one time password that Keyfactor Command generates when triggering a Management-Add job will be used to encrypt the private key and this password will be stored as a secret in GCP Secret Manager with a name of Alias + Password Secret Location Suffix.  For example, if the certificate alias is set as "Alias1" and the Password Secret Location Suffix is set as "_Key", the certificate and encrypted private key will be stored in a secret named "Alias1" and the password for the key encryption will be stored in a secret named "Alias1_Key".  Please note that if using the generated password Keyfactor Command provides and storing the password in Secret Manager, each renewal/replacement of a certificate will encrypt the private key with a new generated password, which will then be stored as a new version of the password secret.
