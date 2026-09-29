// Small hand-rolled inline SVG icon set for the sidebar - no icon library dependency.
import type { SVGProps } from 'react'

function Icon(props: SVGProps<SVGSVGElement>) {
  return (
    <svg
      width="20"
      height="20"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.7"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      {...props}
    />
  )
}

export function PersonIcon() {
  return (
    <Icon>
      <circle cx="12" cy="8" r="3.5" />
      <path d="M4.5 20c1.4-3.8 4.4-6 7.5-6s6.1 2.2 7.5 6" />
    </Icon>
  )
}

export function ChartIcon() {
  return (
    <Icon>
      <path d="M4 20V10M10 20V4M16 20v-7M20 20h-1.5" />
      <path d="M3 20h18" />
    </Icon>
  )
}

export function TruckIcon() {
  return (
    <Icon>
      <rect x="2.5" y="7" width="11" height="9" rx="1" />
      <path d="M13.5 10h4l3 3v3h-7z" />
      <circle cx="7" cy="18.5" r="1.6" />
      <circle cx="16.5" cy="18.5" r="1.6" />
    </Icon>
  )
}

export function DollarIcon() {
  return (
    <Icon>
      <circle cx="12" cy="12" r="9" />
      <path d="M12 6.5v11M15 9.2c0-1.2-1.3-2.2-3-2.2s-3 .9-3 2.1c0 3 6 1.5 6 4.4 0 1.3-1.3 2.2-3 2.2s-3-1-3-2.2" />
    </Icon>
  )
}

export function WrenchIcon() {
  return (
    <Icon>
      <path d="M14.7 6.3a4 4 0 0 0-5.4 4.6L3 17.2V21h3.8l6.3-6.3a4 4 0 0 0 4.6-5.4l-2.7 2.7-2-2 2.7-2.7z" />
    </Icon>
  )
}

export function UsersIcon() {
  return (
    <Icon>
      <circle cx="9" cy="8" r="3.2" />
      <path d="M2.8 19c1.1-3.2 3.5-5 6.2-5s5.1 1.8 6.2 5" />
      <circle cx="17" cy="8.5" r="2.4" />
      <path d="M15.5 14.2c2.3.4 4 1.9 4.8 4.3" />
    </Icon>
  )
}

export function GearIcon() {
  return (
    <Icon>
      <circle cx="12" cy="12" r="3" />
      <path d="M19.4 13.5a1.7 1.7 0 0 0 .3 1.9l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.9-.3 1.7 1.7 0 0 0-1 1.6v.2a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.6 1.7 1.7 0 0 0-1.9.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.9 1.7 1.7 0 0 0-1.6-1h-.2a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.6-1.1 1.7 1.7 0 0 0-.3-1.9l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.9.3h.1a1.7 1.7 0 0 0 1-1.6v-.2a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.6h.1a1.7 1.7 0 0 0 1.9-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.9v.1a1.7 1.7 0 0 0 1.6 1h.2a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.6 1z" />
    </Icon>
  )
}

export function PersonPlusIcon() {
  return (
    <Icon>
      <circle cx="9" cy="8" r="3.5" />
      <path d="M2.5 20c1.3-3.8 4-6 6.5-6s5.2 2.2 6.5 6" />
      <path d="M18.5 8v6M15.5 11h6" />
    </Icon>
  )
}

export function ShieldIcon() {
  return (
    <Icon>
      <path d="M12 3.5l7 2.6v5.4c0 4.5-2.9 7.9-7 9-4.1-1.1-7-4.5-7-9V6.1z" />
      <path d="M9 12l2 2 4-4.2" />
    </Icon>
  )
}

export function MenuIcon() {
  return (
    <Icon strokeWidth="1.8">
      <path d="M3 6h18M3 12h18M3 18h18" />
    </Icon>
  )
}

export function ChevronDownIcon() {
  return (
    <Icon width={14} height={14} strokeWidth="1.8">
      <path d="M4 7l6 6 6-6" />
    </Icon>
  )
}
