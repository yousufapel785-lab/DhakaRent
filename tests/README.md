## Rental workflow browser test

Run against a new local test database with the current schema. The test expects empty rental tables and one seeded admin. It creates tenants, listings, leases, payments and subscription records. Enable private uploads on the test host with `PrivateDocuments__PolicyVersion=test` and `PrivateDocuments__RetentionDays=30`.

Install Python Playwright and its Chromium browser. Supply the local base URL in `DHAKARENT_TEST_URL` and the test admin credentials through `DHAKARENT_TEST_ADMIN_EMAIL` and `DHAKARENT_TEST_ADMIN_PASSWORD`. Set `CHROMIUM_PATH` if using a system Chromium installation.

```sh
python tests/rental_workflow.py
```

The test covers listing uploads and edits, approval and media visibility, a partial payment, maintenance, tenant document review, access revocation and subscription limits. Map tiles are intercepted so the test can run offline.
