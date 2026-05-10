<script setup lang="ts">
import { computed } from 'vue';
import { AlertTriangle, CalendarDays, Copy, Fingerprint, KeyRound, RotateCw, ShieldCheck, Trash2, X } from 'lucide-vue-next';

import type { CertificateItem } from '@/api/types';
import { displayDnsName, formatDateTime, getCategoryLabel, getCertificateCategory, getValidityDays } from '@/utils/certificates';

import StatusBadge from './StatusBadge.vue';

const props = defineProps<{
  certificate: CertificateItem | null;
  open: boolean;
  busy: boolean;
}>();

const emit = defineEmits<{
  close: [];
  renew: [certificate: CertificateItem];
  revoke: [certificate: CertificateItem];
  copy: [label: string, value: string];
}>();

const customTags = computed(() => Object.entries(props.certificate?.tags ?? {}).toSorted(([left], [right]) => left.localeCompare(right)));
const customTagsCopyText = computed(() => customTags.value.map(([key, value]) => `${key}: ${value}`).join('\n'));
</script>

<template>
  <Teleport to="body">
    <div v-if="open && certificate" class="drawer-shell" role="dialog" aria-modal="true" aria-labelledby="certificate-details-heading">
      <button class="drawer-scrim" type="button" title="Close details" @click="emit('close')"></button>
      <aside class="drawer">
        <header class="drawer__header">
          <div>
            <div class="eyebrow">{{ getCategoryLabel(getCertificateCategory(certificate)) }}</div>
            <h2 id="certificate-details-heading">{{ certificate.name }}</h2>
          </div>
          <button class="icon-only-button" type="button" title="Close details" @click="emit('close')">
            <X :size="18" aria-hidden="true" />
          </button>
        </header>

        <div class="drawer__status-line">
          <StatusBadge :certificate="certificate" />
          <span>{{ formatDateTime(certificate.expiresOn) }}</span>
        </div>

        <section class="detail-section">
          <div class="detail-section__heading">
            <h3>DNS Names</h3>
            <button class="copy-button" type="button" title="Copy DNS names" @click="emit('copy', 'DNS names', certificate.dnsNames.join('\n'))">
              <Copy :size="15" aria-hidden="true" />
            </button>
          </div>
          <div class="dns-list dns-list--stacked">
            <span v-for="dnsName in certificate.dnsNames" :key="dnsName" class="dns-chip">{{ displayDnsName(dnsName) }}</span>
          </div>
        </section>

        <section class="detail-grid" aria-label="Certificate metadata">
          <div class="detail-item">
            <CalendarDays :size="17" aria-hidden="true" />
            <div>
              <span>Created</span>
              <strong>{{ formatDateTime(certificate.createdOn) }}</strong>
            </div>
          </div>
          <div class="detail-item">
            <ShieldCheck :size="17" aria-hidden="true" />
            <div>
              <span>Validity</span>
              <strong>{{ getValidityDays(certificate) }} days</strong>
            </div>
          </div>
          <div class="detail-item">
            <KeyRound :size="17" aria-hidden="true" />
            <div>
              <span>Key</span>
              <strong>{{ certificate.keyType ?? '-' }} {{ certificate.keySize ? `${certificate.keySize} bit` : certificate.keyCurveName ?? '' }}</strong>
            </div>
          </div>
          <div class="detail-item detail-item--wide">
            <Fingerprint :size="17" aria-hidden="true" />
            <div class="detail-item__body">
              <span>Thumbprint</span>
              <strong>{{ certificate.x509Thumbprint ?? '-' }}</strong>
            </div>
            <button v-if="certificate.x509Thumbprint" class="copy-button" type="button" title="Copy thumbprint" @click="emit('copy', 'Thumbprint', certificate.x509Thumbprint)">
              <Copy :size="15" aria-hidden="true" />
            </button>
          </div>
        </section>

        <section class="detail-section">
          <h3>Metadata</h3>
          <dl class="metadata-list">
            <div class="metadata-row">
              <dt>Issuer</dt>
              <dd>
                <span class="metadata-chip" :class="certificate.isIssuedByAcmebot ? 'metadata-chip--success' : 'metadata-chip--neutral'">
                  {{ certificate.isIssuedByAcmebot ? 'Acmebot' : 'External' }}
                </span>
              </dd>
            </div>
            <div class="metadata-row">
              <dt>Current ACME endpoint</dt>
              <dd>
                <span class="metadata-chip" :class="certificate.isSameEndpoint ? 'metadata-chip--success' : 'metadata-chip--warning'">
                  {{ certificate.isSameEndpoint ? 'Current' : 'Different' }}
                </span>
              </dd>
            </div>
            <div class="metadata-row">
              <dt>Reuse key</dt>
              <dd>
                <span class="metadata-chip" :class="certificate.reuseKey ? 'metadata-chip--success' : 'metadata-chip--neutral'">
                  {{ certificate.reuseKey ? 'Enabled' : 'Disabled' }}
                </span>
              </dd>
            </div>
            <div v-if="certificate.dnsProviderName" class="metadata-row">
              <dt>DNS provider</dt>
              <dd>{{ certificate.dnsProviderName }}</dd>
            </div>
            <div v-if="certificate.dnsAlias" class="metadata-row metadata-row--stacked">
              <dt>DNS alias</dt>
              <dd class="metadata-value-line">
                <span class="metadata-value">{{ displayDnsName(certificate.dnsAlias ?? '') }}</span>
              </dd>
            </div>
            <div v-if="certificate.acmeEndpoint" class="metadata-row metadata-row--stacked">
              <dt>ACME endpoint</dt>
              <dd class="metadata-value-line">
                <span class="metadata-value metadata-value--mono">{{ certificate.acmeEndpoint }}</span>
              </dd>
            </div>
          </dl>
        </section>

        <section v-if="customTags.length > 0" class="detail-section">
          <div class="detail-section__heading">
            <h3>Tags</h3>
            <button class="copy-button" type="button" title="Copy tags" @click="emit('copy', 'Tags', customTagsCopyText)">
              <Copy :size="15" aria-hidden="true" />
            </button>
          </div>
          <dl class="metadata-list">
            <div v-for="[key, value] in customTags" :key="key" class="metadata-row metadata-row--stacked">
              <dt>{{ key }}</dt>
              <dd class="metadata-value-line">
                <span class="metadata-value">{{ value || '-' }}</span>
              </dd>
            </div>
          </dl>
        </section>

        <footer class="drawer__footer">
          <button class="primary-button" type="button" :disabled="busy" @click="emit('renew', certificate)">
            <RotateCw :size="17" aria-hidden="true" />
            <span>Renew</span>
          </button>
          <button
            v-if="certificate.isIssuedByAcmebot && certificate.isSameEndpoint && !certificate.isExpired"
            class="danger-button"
            type="button"
            :disabled="busy"
            @click="emit('revoke', certificate)"
          >
            <Trash2 :size="17" aria-hidden="true" />
            <span>Revoke</span>
          </button>
          <div v-else class="drawer__hint">
            <AlertTriangle :size="15" aria-hidden="true" />
            Revoke is unavailable for this certificate.
          </div>
        </footer>
      </aside>
    </div>
  </Teleport>
</template>
