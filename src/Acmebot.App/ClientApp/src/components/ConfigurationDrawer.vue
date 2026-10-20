<script setup lang="ts">
import { computed } from 'vue';
import { AlertTriangle, LoaderCircle, RefreshCw, Settings, X } from 'lucide-vue-next';
import { FocusScope } from 'reka-ui';

import type { ConfigurationInfo } from '@/api/types';

const props = defineProps<{
  configuration: ConfigurationInfo | null;
  open: boolean;
  loading: boolean;
  error: string;
}>();

const emit = defineEmits<{
  close: [];
  refresh: [];
}>();

const sections = computed(() => props.configuration
  ? [props.configuration.acme, props.configuration.general, ...props.configuration.dnsProviders]
  : []);
</script>

<template>
  <Teleport to="body">
    <div
      v-if="open"
      class="drawer-shell"
      role="dialog"
      aria-modal="true"
      aria-labelledby="configuration-heading"
      @keydown.esc="emit('close')"
    >
      <button
        class="drawer-scrim"
        type="button"
        aria-label="Close configuration"
        @click="emit('close')"
      />
      <FocusScope
        as-child
        trapped
        loop
      >
        <aside class="drawer">
          <header class="drawer__header">
            <div>
              <div class="eyebrow">
                Settings
              </div>
              <h2 id="configuration-heading">
                Current configuration
              </h2>
            </div>
            <button
              class="icon-only-button"
              type="button"
              aria-label="Close configuration"
              @click="emit('close')"
            >
              <X
                :size="18"
                aria-hidden="true"
              />
            </button>
          </header>

          <div class="drawer__status-line">
            <Settings
              :size="16"
              aria-hidden="true"
            />
            <span>Read-only configuration</span>
          </div>

          <section
            v-if="loading"
            class="drawer-state"
            role="status"
          >
            <LoaderCircle
              class="spin"
              :size="24"
              aria-hidden="true"
            />
            <strong>Loading configuration</strong>
          </section>
          <section
            v-else-if="error"
            class="drawer-state drawer-state--error"
            role="alert"
          >
            <AlertTriangle
              :size="24"
              aria-hidden="true"
            />
            <div>
              <strong>Failed to load configuration</strong>
              <p>{{ error }}</p>
            </div>
          </section>
          <template v-else-if="configuration">
            <section class="detail-section">
              Settings currently loaded by Acmebot. Change settings in the Function App configuration.
              Credentials are shown only as configured or not configured; this does not verify connectivity.
              Blank managed identity client IDs use the default identity.
            </section>
            <section
              v-for="section in sections"
              :key="section.name"
              class="detail-section"
            >
              <h3>{{ section.name }}</h3>
              <dl class="metadata-list">
                <div
                  v-for="setting in section.settings"
                  :key="setting.name"
                  class="metadata-row metadata-row--stacked"
                >
                  <dt>{{ setting.name }}</dt>
                  <dd class="metadata-value">
                    {{ setting.isConfigured === null ? (setting.value ?? 'Not set') : (setting.isConfigured ? 'Configured' : 'Not configured') }}
                  </dd>
                </div>
              </dl>
            </section>
            <section
              v-if="configuration.dnsProviders.length === 0"
              class="detail-section"
            >
              <h3>DNS providers</h3>
              <p>No DNS providers are configured.</p>
            </section>
          </template>

          <footer class="drawer__footer">
            <button
              class="secondary-button"
              type="button"
              :disabled="loading"
              @click="emit('refresh')"
            >
              <RefreshCw
                :size="16"
                aria-hidden="true"
              />
              Refresh
            </button>
            <button
              class="secondary-button"
              type="button"
              @click="emit('close')"
            >
              Close
            </button>
          </footer>
        </aside>
      </FocusScope>
    </div>
  </Teleport>
</template>
