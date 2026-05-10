<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { CirclePlus, KeyRound, Plus, ShieldPlus, Trash2, X } from 'lucide-vue-next';
import { toASCII } from 'punycode/';

import type { CertificatePolicyItem, DnsZoneGroup, KeyCurveName, KeyType, SelectableDnsZone } from '@/api/types';
import { displayDnsName } from '@/utils/certificates';

import SearchableZoneSelect from './SearchableZoneSelect.vue';

const props = defineProps<{
  open: boolean;
  zones: DnsZoneGroup[];
  loadingZones: boolean;
  sending: boolean;
}>();

const emit = defineEmits<{
  close: [];
  submit: [policy: CertificatePolicyItem];
  'load-zones': [];
}>();

const selectedZone = ref<SelectableDnsZone | null>(null);
const formMessage = ref('');

const form = reactive({
  recordName: '',
  dnsNames: [] as string[],
  dnsProviderName: '',
  useAdvancedOptions: false,
  certificateName: '',
  keyType: 'RSA' as KeyType,
  keySize: 2048,
  keyCurveName: 'P-256' as KeyCurveName,
  reuseKey: false,
  dnsAlias: ''
});

const canSubmit = computed(() => form.dnsNames.length > 0 && !props.sending);
const fullDnsName = computed(() => buildDnsName());
const keySummary = computed(() => (form.keyType === 'RSA' ? `${form.keySize} bit RSA` : `${form.keyCurveName} EC`));
const issueStatusLabel = computed(() => {
  if (form.dnsNames.length > 0) {
    return 'Ready';
  }

  if (selectedZone.value) {
    return 'Add DNS name';
  }

  return 'Select zone';
});
const dnsNamesSummary = computed(() => {
  if (form.dnsNames.length === 0) {
    return 'None added';
  }

  const firstDnsName = displayDnsName(form.dnsNames[0]);

  if (form.dnsNames.length === 1) {
    return firstDnsName;
  }

  return `${firstDnsName} +${form.dnsNames.length - 1}`;
});
const dnsNameCountLabel = computed(() => `${form.dnsNames.length} ${form.dnsNames.length === 1 ? 'DNS name' : 'DNS names'}`);

watch(
  () => props.open,
  (open) => {
    if (open) {
      resetForm();
      emit('load-zones');
    }
  }
);

function resetForm(): void {
  selectedZone.value = null;
  formMessage.value = '';
  form.recordName = '';
  form.dnsNames = [];
  form.dnsProviderName = '';
  form.useAdvancedOptions = false;
  form.certificateName = '';
  form.keyType = 'RSA';
  form.keySize = 2048;
  form.keyCurveName = 'P-256';
  form.reuseKey = false;
  form.dnsAlias = '';
}

function normalizeRecordName(recordName: string): string {
  return recordName.trim().replace(/\.$/, '');
}

function buildDnsName(): string | null {
  if (!selectedZone.value) {
    return null;
  }

  const normalizedRecordName = normalizeRecordName(form.recordName);

  if (!normalizedRecordName || normalizedRecordName === '@') {
    return selectedZone.value.name;
  }

  if (normalizedRecordName.endsWith(`.${selectedZone.value.name}`)) {
    return toASCII(normalizedRecordName);
  }

  return `${toASCII(normalizedRecordName)}.${selectedZone.value.name}`;
}

function addDnsName(): void {
  formMessage.value = '';

  if (!selectedZone.value) {
    return;
  }

  if (form.dnsProviderName && form.dnsProviderName !== selectedZone.value.dnsProviderName) {
    formMessage.value = 'DNS names in one certificate must use the same DNS provider.';
    return;
  }

  const dnsName = buildDnsName();

  if (!dnsName) {
    return;
  }

  if (!form.dnsNames.includes(dnsName)) {
    form.dnsNames.push(dnsName);
  } else {
    formMessage.value = 'This DNS name is already in the certificate.';
    return;
  }

  form.dnsProviderName = selectedZone.value.dnsProviderName;
  form.recordName = '';
}

function removeDnsName(dnsName: string): void {
  form.dnsNames = form.dnsNames.filter((candidate) => candidate !== dnsName);

  if (form.dnsNames.length === 0) {
    form.dnsProviderName = '';
    formMessage.value = '';
  }
}

function submit(): void {
  if (!canSubmit.value) {
    return;
  }

  const policy: CertificatePolicyItem = {
    dnsNames: form.dnsNames,
    dnsProviderName: form.dnsProviderName || undefined,
    certificateName: form.certificateName.trim() || undefined,
    keyType: form.keyType,
    reuseKey: form.reuseKey,
    dnsAlias: form.dnsAlias.trim() || undefined
  };

  if (form.keyType === 'RSA') {
    policy.keySize = form.keySize;
  } else {
    policy.keyCurveName = form.keyCurveName;
  }

  emit('submit', policy);
}
</script>

<template>
  <Teleport to="body">
    <div v-if="open" class="modal-shell" role="dialog" aria-modal="true" aria-labelledby="add-certificate-heading">
      <button class="modal-scrim" type="button" title="Close issue certificate" :disabled="sending" @click="emit('close')"></button>
      <section class="modal-panel modal-panel--wide">
        <header class="modal-panel__header">
          <div>
            <div class="eyebrow">Certificate issuance</div>
            <h2 id="add-certificate-heading">Issue Certificate</h2>
          </div>
          <button class="icon-only-button" type="button" title="Close issue certificate" :disabled="sending" @click="emit('close')">
            <X :size="18" aria-hidden="true" />
          </button>
        </header>

        <div class="wizard-layout">
          <aside class="setup-rail" aria-label="Certificate issue setup">
            <div class="setup-rail__header">
              <span>Issue setup</span>
              <strong>{{ issueStatusLabel }}</strong>
            </div>
            <div class="setup-step" :class="{ 'is-complete': selectedZone }">
              <ShieldPlus :size="17" aria-hidden="true" />
              <div class="setup-step__body">
                <span>Zone</span>
                <strong>{{ selectedZone ? displayDnsName(selectedZone.name) : 'Not selected' }}</strong>
                <small v-if="selectedZone">{{ selectedZone.dnsProviderName }}</small>
              </div>
            </div>
            <div class="setup-step" :class="{ 'is-complete': form.dnsNames.length > 0 }">
              <CirclePlus :size="17" aria-hidden="true" />
              <div class="setup-step__body">
                <span>Names</span>
                <strong>{{ dnsNamesSummary }}</strong>
                <small>{{ dnsNameCountLabel }}</small>
              </div>
            </div>
            <div class="setup-step" :class="{ 'is-complete': form.useAdvancedOptions }">
              <KeyRound :size="17" aria-hidden="true" />
              <div class="setup-step__body">
                <span>Key</span>
                <strong>{{ keySummary }}</strong>
                <small>{{ form.useAdvancedOptions ? 'Custom settings' : 'Default settings' }}</small>
              </div>
            </div>
          </aside>

          <div class="wizard-body">
            <div class="form-section">
              <label class="form-label">DNS Zone</label>
              <SearchableZoneSelect v-model:selected="selectedZone" :groups="zones" :loading="loadingZones" />
            </div>

            <div class="form-section">
              <label class="form-label" for="record-name">DNS Name</label>
              <div class="compound-input">
                <input
                  id="record-name"
                  v-model="form.recordName"
                  type="text"
                  placeholder="@, www, api, *"
                  :disabled="!selectedZone"
                  @keydown.enter.prevent="addDnsName"
                />
                <span class="compound-input__suffix">.{{ selectedZone ? displayDnsName(selectedZone.name) : 'zone' }}</span>
                <button class="icon-button" type="button" :disabled="!selectedZone" @click="addDnsName">
                  <Plus :size="16" aria-hidden="true" />
                  <span>Add</span>
                </button>
              </div>
              <div v-if="fullDnsName" class="form-result">
                <span>Full DNS name</span>
                <strong>{{ displayDnsName(fullDnsName) }}</strong>
              </div>
              <p v-if="formMessage" class="form-error">{{ formMessage }}</p>
              <div class="dns-list dns-list--editable">
                <span v-for="dnsName in form.dnsNames" :key="dnsName" class="dns-chip dns-chip--removable">
                  {{ displayDnsName(dnsName) }}
                  <button type="button" title="Remove DNS name" @click="removeDnsName(dnsName)">
                    <Trash2 :size="13" aria-hidden="true" />
                  </button>
                </span>
              </div>
            </div>

            <div class="form-section form-section--inline">
              <label class="toggle-row">
                <input v-model="form.useAdvancedOptions" type="checkbox" />
                <span>Advanced options</span>
              </label>
            </div>

            <div v-if="form.useAdvancedOptions" class="advanced-grid">
              <label class="form-field">
                <span class="form-label">Certificate Name</span>
                <input v-model="form.certificateName" type="text" placeholder="Optional certificate name" />
              </label>

              <label class="form-field">
                <span class="form-label">Key Type</span>
                <select v-model="form.keyType">
                  <option value="RSA">RSA</option>
                  <option value="EC">EC</option>
                </select>
              </label>

              <label v-if="form.keyType === 'RSA'" class="form-field">
                <span class="form-label">Key Size</span>
                <select v-model.number="form.keySize">
                  <option :value="2048">2048</option>
                  <option :value="3072">3072</option>
                  <option :value="4096">4096</option>
                </select>
              </label>

              <label v-else class="form-field">
                <span class="form-label">Curve</span>
                <select v-model="form.keyCurveName">
                  <option value="P-256">P-256</option>
                  <option value="P-384">P-384</option>
                  <option value="P-521">P-521</option>
                  <option value="P-256K">P-256K</option>
                </select>
              </label>

              <label class="form-field">
                <span class="form-label">DNS Alias</span>
                <input v-model="form.dnsAlias" type="text" placeholder="alias.example.com" />
              </label>

              <label class="toggle-row advanced-grid__toggle">
                <input v-model="form.reuseKey" type="checkbox" />
                <span>Reuse key on renewal</span>
              </label>
            </div>
          </div>
        </div>

        <footer class="modal-panel__footer">
          <div class="modal-panel__footer-meta">
            <span>{{ dnsNameCountLabel }}</span>
            <span>{{ keySummary }}</span>
          </div>
          <div class="modal-panel__footer-actions">
            <button class="secondary-button" type="button" :disabled="sending" @click="emit('close')">Cancel</button>
            <button class="primary-button" type="button" :disabled="!canSubmit" @click="submit">
              <ShieldPlus :size="17" aria-hidden="true" />
              <span>Issue Certificate</span>
            </button>
          </div>
        </footer>
      </section>
    </div>
  </Teleport>
</template>
