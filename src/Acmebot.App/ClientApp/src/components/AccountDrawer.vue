<script setup lang="ts">
import { AlertTriangle, Copy, Info, LoaderCircle, RefreshCw, ShieldCheck, X } from 'lucide-vue-next';

import type { AccountInfo } from '@/api/types';

defineProps<{
  account: AccountInfo | null;
  open: boolean;
  loading: boolean;
  error: string;
}>();

const emit = defineEmits<{
  close: [];
  copy: [label: string, value: string];
  retry: [];
}>();
</script>

<template>
  <Teleport to="body">
    <div
      v-if="open"
      class="drawer-shell"
      role="dialog"
      aria-modal="true"
      aria-labelledby="account-details-heading"
    >
      <button
        class="drawer-scrim"
        type="button"
        title="Close account information"
        aria-label="Close account information"
        @click="emit('close')"
      />
      <aside class="drawer">
        <header class="drawer__header">
          <div>
            <div class="eyebrow">
              ACME
            </div>
            <h2 id="account-details-heading">
              Account information
            </h2>
          </div>
          <button
            class="icon-only-button"
            type="button"
            title="Close account information"
            aria-label="Close account information"
            @click="emit('close')"
          >
            <X
              :size="18"
              aria-hidden="true"
            />
          </button>
        </header>

        <div class="drawer__status-line">
          <ShieldCheck
            :size="16"
            aria-hidden="true"
          />
          <span>Read-only account and issuer metadata</span>
        </div>

        <section
          v-if="loading && !account"
          class="drawer-state"
          role="status"
        >
          <LoaderCircle
            class="spin"
            :size="24"
            aria-hidden="true"
          />
          <div>
            <strong>Loading account information</strong>
            <p>The ACME account is being prepared.</p>
          </div>
        </section>

        <section
          v-else-if="error && !account"
          class="drawer-state drawer-state--error"
          role="alert"
        >
          <AlertTriangle
            :size="24"
            aria-hidden="true"
          />
          <div>
            <strong>Failed to load account information</strong>
            <p>{{ error }}</p>
            <button
              class="secondary-button"
              type="button"
              @click="emit('retry')"
            >
              <RefreshCw
                :size="16"
                aria-hidden="true"
              />
              Retry
            </button>
          </div>
        </section>

        <template v-else-if="account">
          <section class="detail-section">
            <h3>ACME directory</h3>
            <dl class="metadata-list">
              <div class="metadata-row metadata-row--stacked">
                <dt>Directory URL</dt>
                <dd class="metadata-value-line">
                  <span class="metadata-value metadata-value--mono">{{ account.directoryUrl }}</span>
                  <button
                    class="copy-button"
                    type="button"
                    title="Copy directory URL"
                    aria-label="Copy directory URL"
                    @click="emit('copy', 'Directory URL', account.directoryUrl)"
                  >
                    <Copy
                      :size="15"
                      aria-hidden="true"
                    />
                  </button>
                </dd>
              </div>
            </dl>
          </section>

          <section class="detail-section">
            <h3>CAA identities</h3>
            <div
              v-if="account.caaIdentities.length === 0"
              class="drawer-empty"
            >
              <Info
                :size="20"
                aria-hidden="true"
              />
              <div>
                <strong>No CAA identities advertised</strong>
                <p>The ACME directory did not provide any CAA identities.</p>
              </div>
            </div>
            <ul
              v-else
              class="account-identity-list"
            >
              <li
                v-for="(identity, index) in account.caaIdentities"
                :key="`${identity}-${index}`"
                class="account-identity-row"
              >
                <span class="metadata-value metadata-value--mono">{{ identity }}</span>
                <button
                  class="copy-button"
                  type="button"
                  :title="`Copy CAA identity ${identity}`"
                  :aria-label="`Copy CAA identity ${identity}`"
                  @click="emit('copy', 'CAA identity', identity)"
                >
                  <Copy
                    :size="15"
                    aria-hidden="true"
                  />
                </button>
              </li>
            </ul>
          </section>

          <section class="detail-section">
            <h3>DNS-PERSIST-01</h3>
            <dl class="metadata-list">
              <div class="metadata-row metadata-row--stacked">
                <dt>CA pre-provisioning metadata</dt>
                <dd>{{ account.dnsPersist01 ? (account.dnsPersist01.preProvisioningMetadataAvailable ? 'Available' : 'Not available') : 'Unknown (server did not report metadata)' }}</dd>
              </div>
              <template v-if="account.dnsPersist01">
                <div class="metadata-row metadata-row--stacked">
                  <dt>Issuer domain names</dt>
                  <dd class="metadata-value metadata-value--mono">
                    {{ account.dnsPersist01.issuerDomainNames.join(', ') || 'None advertised' }}
                  </dd>
                </div>
                <div class="metadata-row metadata-row--stacked">
                  <dt>Account hash prefix</dt>
                  <dd class="metadata-value metadata-value--mono">
                    {{ account.dnsPersist01.accountHashPrefix ?? 'None advertised' }}
                  </dd>
                </div>
              </template>
            </dl>
            <p class="account-drawer__note">
              Acmebot does not support DNS-PERSIST-01 issuance or record generation yet. Keep DNS-01 provider permissions in place.
            </p>
          </section>

          <section class="detail-section account-drawer__note">
            Values are shown exactly as advertised by the ACME server.
          </section>
        </template>

        <footer class="drawer__footer">
          <button
            class="secondary-button"
            type="button"
            @click="emit('close')"
          >
            Close
          </button>
        </footer>
      </aside>
    </div>
  </Teleport>
</template>
