import { useStore } from '../../lib/useStore.js';
import {
  FiltersModal, SearchModal, DatesModal, GuestsModal, LanguageModal,
  HelpModal, ReportModal, ShareModal, ContactHostModal
} from './SearchModals.jsx';
import { AuthModal, ProfileModal, ReviewModal, CancelTripModal } from './AccountModals.jsx';
import { PhotosModal, AmenitiesModal, ReviewsModal, CheckoutModal } from './ListingModals.jsx';
import { HostCalendarModal, GuestReviewModal, HostCancelModal } from './HostModals.jsx';
import { ListingWizard } from './ListingWizard.jsx';
import { GuidebookEditor } from './GuidebookEditor.jsx';
import { ExperienceEditor } from './ExperienceEditor.jsx';
import { ServiceEditor } from './ServiceEditor.jsx';
import { ShieldModal } from './ShieldModal.jsx';

const REGISTRY = {
  filters: FiltersModal,
  search: SearchModal,
  dates: DatesModal,
  guests: GuestsModal,
  language: LanguageModal,
  help: HelpModal,
  report: ReportModal,
  share: ShareModal,
  'contact-host': ContactHostModal,
  login: AuthModal,
  profile: ProfileModal,
  review: ReviewModal,
  'cancel-trip': CancelTripModal,
  photos: PhotosModal,
  amenities: AmenitiesModal,
  reviews: ReviewsModal,
  checkout: CheckoutModal,
  'listing-editor': ListingWizard,
  'guidebook-editor': GuidebookEditor,
  'experience-editor': ExperienceEditor,
  'service-editor': ServiceEditor,
  'host-block': HostCalendarModal,
  'guest-review': GuestReviewModal,
  'host-cancel': HostCancelModal,
  shield: ShieldModal
};

export function Overlay() {
  const state = useStore();
  const Body = REGISTRY[state.overlay];
  // Keying on the overlay name remounts on every switch, so each modal's local
  // form state starts clean instead of leaking into the next one.
  return Body ? <Body key={state.overlay} /> : null;
}
