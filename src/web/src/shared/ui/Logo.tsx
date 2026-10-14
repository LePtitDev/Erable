import { Leaf } from "lucide-react";

/** Provisional application mark (a proper logo arrives with the release work, M5-03). */
export function Logo({ size = 22 }: { readonly size?: number }) {
  return <Leaf aria-hidden size={size} strokeWidth={2.2} />;
}
