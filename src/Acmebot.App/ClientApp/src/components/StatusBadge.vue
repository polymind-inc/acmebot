<script setup lang="ts">
import { computed } from 'vue';

import type { CertificateItem } from '@/api/types';
import { getCertificateStatus } from '@/utils/certificates';

const props = defineProps<{
  certificate: CertificateItem;
}>();

const status = computed(() => getCertificateStatus(props.certificate));
</script>

<template>
  <span
    class="status-badge"
    :class="`status-badge--${status.kind}`"
  >
    <span
      class="status-badge__dot"
      aria-hidden="true"
    />
    {{ status.label }}
  </span>
</template>
